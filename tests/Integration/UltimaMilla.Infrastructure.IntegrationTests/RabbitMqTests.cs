using System.Text;
using System.Diagnostics.Metrics;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Infrastructure.Mensajeria;
using UltimaMilla.Infrastructure.Observabilidad;

namespace UltimaMilla.Infrastructure.IntegrationTests;

// Usan nombres únicos por prueba (exchange y colas propios) y los borran al terminar.
public sealed class RabbitMqTests : IAsyncLifetime
{
    private readonly OpcionesRabbitMq _opciones = new()
    {
        Host = RabbitMqFactAttribute.Variable("HOST") ?? "127.0.0.1",
        Puerto = int.TryParse(RabbitMqFactAttribute.Variable("PUERTO"), out var p) ? p : 55672,
        Usuario = RabbitMqFactAttribute.Variable("USUARIO") ?? "ultimamilla",
        Clave = RabbitMqFactAttribute.Variable("CLAVE") ?? "",
    };
    private readonly ConexionRabbitMq _conexion;
    private readonly TransporteRabbitMq _transporte;

    public RabbitMqTests()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        _opciones.Exchange = $"test.{sufijo}.eventos";
        _opciones.ColaAvisos = $"test.{sufijo}.avisos";
        _conexion = new ConexionRabbitMq(_opciones);
        _transporte = new TransporteRabbitMq(_conexion, _opciones);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_opciones.Clave)) return; // Prueba omitida: nunca se conectó.
        var amqp = await _conexion.ObtenerAsync();
        await using var canal = await amqp.CreateChannelAsync();
        await canal.ExchangeDeleteAsync(_opciones.Exchange);
        foreach (var cola in Colas()) await canal.QueueDeleteAsync(cola);
        await _transporte.DisposeAsync();
        await _conexion.DisposeAsync();
    }

    private IEnumerable<string> Colas() =>
        new[] { _opciones.ColaAvisos, _opciones.ColaMuertos }.Concat(_opciones.Esperas.Select(e => _opciones.ColaEspera(e.Sufijo)));

    private static MensajeParaPublicar Mensaje(string tipo = "envio.evento.v1") =>
        new(Guid.NewGuid(), Guid.NewGuid(), tipo, """{"hola":"mundo"}""", DateTime.UtcNow);

    private async Task<BasicGetResult?> EsperarAsync(IChannel canal, string cola, TimeSpan maximo)
    {
        var limite = DateTime.UtcNow + maximo;
        while (DateTime.UtcNow < limite)
        {
            var r = await canal.BasicGetAsync(cola, autoAck: false);
            if (r is not null) return r;
            await Task.Delay(250);
        }
        return null;
    }

    [RabbitMqFact]
    public async Task DeclararTopologiaEsIdempotenteYCreaTodasLasColas()
    {
        var topologia = new TopologiaRabbitMq(_conexion, _opciones);
        await topologia.DeclararAsync();
        await topologia.DeclararAsync();

        var amqp = await _conexion.ObtenerAsync();
        await using var canal = await amqp.CreateChannelAsync();
        foreach (var cola in Colas()) await canal.QueueDeclarePassiveAsync(cola); // lanza si no existe
        await canal.ExchangeDeclarePassiveAsync(_opciones.Exchange);
        Assert.Equal(new[] { "avisos.espera.5s", "avisos.espera.30s", "avisos.espera.2m" },
            _opciones.Esperas.Select(e => "avisos.espera." + e.Sufijo));
    }

    [RabbitMqFact]
    public async Task PublicaPersistenteConMessageIdYRoutingKeyIgualAlTipo()
    {
        await new TopologiaRabbitMq(_conexion, _opciones).DeclararAsync();
        var mensaje = Mensaje() with
        {
            TraceParent = "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01",
            TraceState = "prueba=publicacion"
        };
        await _transporte.PublicarConfirmadoAsync(mensaje);

        long? listos = null;
        using var metricas = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "ultimamilla.cola.profundidad") listener.EnableMeasurementEvents(instrument);
            }
        };
        metricas.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "cola" && Equals(tag.Value, _opciones.ColaAvisos)) listos = value;
        });
        metricas.Start();
        await new SondeoMetricasOperativas("", _conexion, _opciones).MedirColasAsync(CancellationToken.None);
        Assert.Equal(1, listos);

        var amqp = await _conexion.ObtenerAsync();
        await using var canal = await amqp.CreateChannelAsync();
        var recibido = await EsperarAsync(canal, _opciones.ColaAvisos, TimeSpan.FromSeconds(5));
        Assert.NotNull(recibido);
        Assert.Equal(mensaje.MensajeId.ToString(), recibido.BasicProperties.MessageId);
        Assert.Equal(DeliveryModes.Persistent, recibido.BasicProperties.DeliveryMode);
        Assert.Equal(mensaje.Tipo, recibido.RoutingKey);
        Assert.Equal(_opciones.Exchange, recibido.Exchange);
        Assert.Equal(mensaje.Contenido, Encoding.UTF8.GetString(recibido.Body.ToArray()));
        Assert.Equal(mensaje.TraceParent, Encoding.UTF8.GetString((byte[])recibido.BasicProperties.Headers!["traceparent"]!));
        Assert.Equal(mensaje.TraceState, Encoding.UTF8.GetString((byte[])recibido.BasicProperties.Headers!["tracestate"]!));
        await canal.BasicAckAsync(recibido.DeliveryTag, false);
    }

    [RabbitMqFact]
    public async Task UnMensajeSinRutaFallaEnVezDePerderseEnSilencio()
    {
        await new TopologiaRabbitMq(_conexion, _opciones).DeclararAsync();
        var error = await Assert.ThrowsAsync<MensajeNoPublicableException>(
            () => _transporte.PublicarConfirmadoAsync(Mensaje("otro.tipo")));
        Assert.True(Assert.IsAssignableFrom<PublishException>(error.InnerException).IsReturn);
    }

    [RabbitMqFact]
    public async Task UnMensajeRechazadoSinReencolarVaALaColaDeMuertos()
    {
        await new TopologiaRabbitMq(_conexion, _opciones).DeclararAsync();
        var mensaje = Mensaje();
        await _transporte.PublicarConfirmadoAsync(mensaje);

        var amqp = await _conexion.ObtenerAsync();
        await using var canal = await amqp.CreateChannelAsync();
        var recibido = await EsperarAsync(canal, _opciones.ColaAvisos, TimeSpan.FromSeconds(5));
        Assert.NotNull(recibido);
        await canal.BasicNackAsync(recibido.DeliveryTag, false, requeue: false);

        var muerto = await EsperarAsync(canal, _opciones.ColaMuertos, TimeSpan.FromSeconds(5));
        Assert.NotNull(muerto);
        Assert.Equal(mensaje.MensajeId.ToString(), muerto.BasicProperties.MessageId);
    }

    [RabbitMqFact]
    public async Task LaColaDeEspera5sDevuelveElMensajeAAvisosDespuesDelTtl()
    {
        await new TopologiaRabbitMq(_conexion, _opciones).DeclararAsync();
        var amqp = await _conexion.ObtenerAsync();
        await using var canal = await amqp.CreateChannelAsync();
        var id = Guid.NewGuid().ToString();
        await canal.BasicPublishAsync("", _opciones.ColaEspera("5s"), mandatory: true,
            new BasicProperties { MessageId = id, DeliveryMode = DeliveryModes.Persistent }, "x"u8.ToArray());

        Assert.Null(await canal.BasicGetAsync(_opciones.ColaAvisos, autoAck: true)); // todavía esperando
        var vuelto = await EsperarAsync(canal, _opciones.ColaAvisos, TimeSpan.FromSeconds(15));
        Assert.NotNull(vuelto);
        Assert.Equal(id, vuelto.BasicProperties.MessageId);
    }
}
