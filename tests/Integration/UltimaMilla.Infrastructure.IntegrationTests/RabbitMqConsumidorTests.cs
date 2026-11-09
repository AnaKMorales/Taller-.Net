using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Infrastructure.Mensajeria;

namespace UltimaMilla.Infrastructure.IntegrationTests;

// Nombres únicos por prueba; se borran al terminar.
public sealed class RabbitMqConsumidorTests : IAsyncLifetime
{
    private readonly OpcionesRabbitMq _opciones = new()
    {
        Host = RabbitMqFactAttribute.Variable("HOST") ?? "127.0.0.1",
        Puerto = int.TryParse(RabbitMqFactAttribute.Variable("PUERTO"), out var p) ? p : 55672,
        Usuario = RabbitMqFactAttribute.Variable("USUARIO") ?? "ultimamilla",
        Clave = RabbitMqFactAttribute.Variable("CLAVE") ?? "",
        EsperaCierre = TimeSpan.FromMilliseconds(400),
    };
    private readonly ConexionRabbitMq _conexion;
    private readonly TopologiaRabbitMq _topologia;
    private ConsumidorRabbitMq? _consumidor;

    public RabbitMqConsumidorTests()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        _opciones.Exchange = $"test.{sufijo}.eventos";
        _opciones.ColaAvisos = $"test.{sufijo}.avisos";
        _conexion = new ConexionRabbitMq(_opciones);
        _topologia = new TopologiaRabbitMq(_conexion, _opciones);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_opciones.Clave)) return;
        if (_consumidor is not null) await _consumidor.DisposeAsync();
        var amqp = await _conexion.ObtenerAsync();
        await using var canal = await amqp.CreateChannelAsync();
        await canal.ExchangeDeleteAsync(_opciones.Exchange);
        foreach (var cola in new[] { _opciones.ColaAvisos, _opciones.ColaMuertos }
                     .Concat(_opciones.Esperas.Select(e => _opciones.ColaEspera(e.Sufijo))))
            await canal.QueueDeleteAsync(cola);
        await _conexion.DisposeAsync();
    }

    private sealed class Manejador(Func<MensajeRecibido, CancellationToken, Task<ResultadoConsumo>> accion) : IManejadorMensajes
    {
        public ConcurrentQueue<MensajeRecibido> Recibidos { get; } = new();
        public Task<ResultadoConsumo> ManejarAsync(MensajeRecibido mensaje, CancellationToken cancellationToken = default)
        {
            Recibidos.Enqueue(mensaje);
            return accion(mensaje, cancellationToken);
        }
    }

    private async Task<(Manejador, string)> IniciarAsync(Func<MensajeRecibido, CancellationToken, Task<ResultadoConsumo>> accion,
        int intentosPrevios = 0, Dictionary<string, object?>? cabeceras = null)
    {
        var manejador = new Manejador(accion);
        _consumidor = new ConsumidorRabbitMq(_conexion, _topologia, _opciones, manejador, NullLogger<ConsumidorRabbitMq>.Instance);
        await _consumidor.IniciarAsync();
        var id = Guid.NewGuid().ToString();
        var amqp = await _conexion.ObtenerAsync();
        await using var canal = await amqp.CreateChannelAsync(new CreateChannelOptions(true, true));
        var encabezados = new Dictionary<string, object?>(cabeceras ?? []);
        if (intentosPrevios > 0) encabezados[ConsumidorRabbitMq.EncabezadoIntentos] = intentosPrevios;
        await canal.BasicPublishAsync(_opciones.Exchange, "envio.evento.v1", mandatory: true,
            new BasicProperties { MessageId = id, Type = "envio.evento.v1", DeliveryMode = DeliveryModes.Persistent, Headers = encabezados },
            Encoding.UTF8.GetBytes("""{"x":1}"""));
        return (manejador, id);
    }

    [RabbitMqFact]
    public async Task RecuperaContextoOriginalEnCadaReintentoYSinContextoIniciaUnaTrazaNueva()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DiagnosticoMensajeria.NombreFuente,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        const string traceId = "0123456789abcdef0123456789abcdef";
        const string spanId = "0123456789abcdef";
        var intentos = new ConcurrentQueue<(string TraceId, string ParentId, string SpanId, string? State)>();
        var listo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await IniciarAsync((mensaje, _) =>
        {
            var activity = Activity.Current!;
            intentos.Enqueue((activity.TraceId.ToString(), activity.ParentSpanId.ToString(), activity.SpanId.ToString(),
                activity.TraceStateString));
            if (mensaje.Intentos == 0) return Task.FromResult(ResultadoConsumo.Reintentar);
            listo.TrySetResult();
            return Task.FromResult(ResultadoConsumo.Confirmar);
        }, cabeceras: new()
        {
            ["traceparent"] = $"00-{traceId}-{spanId}-01",
            ["tracestate"] = "prueba=original"
        });
        await listo.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await _consumidor!.DetenerAsync();
        Assert.Equal(2, intentos.Count);
        Assert.All(intentos, intento =>
        {
            Assert.Equal(traceId, intento.TraceId);
            Assert.Equal(spanId, intento.ParentId);
            Assert.Equal("prueba=original", intento.State);
        });
        Assert.Equal(2, intentos.Select(i => i.SpanId).Distinct().Count());
    }

    [RabbitMqFact]
    public async Task CabecerasInvalidasNoImpidenProcesarYNoHeredanUnaTrazaAjena()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DiagnosticoMensajeria.NombreFuente,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        var recibido = new TaskCompletionSource<ActivityContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ajena = new Activity("actividad-ajena").SetIdFormat(ActivityIdFormat.W3C).Start();
        await IniciarAsync((_, _) =>
        {
            recibido.TrySetResult(Activity.Current!.Context);
            return Task.FromResult(ResultadoConsumo.Confirmar);
        }, cabeceras: new() { ["traceparent"] = "invalido", ["tracestate"] = "prueba=ignorada" });
        var contexto = await recibido.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await _consumidor!.DetenerAsync();
        Assert.NotEqual(ajena.TraceId, contexto.TraceId);
        Assert.Null(contexto.TraceState);
        Assert.Equal(ajena, Activity.Current);
    }

    private async Task<BasicGetResult?> EsperarAsync(string cola, TimeSpan maximo)
    {
        var amqp = await _conexion.ObtenerAsync();
        await using var canal = await amqp.CreateChannelAsync();
        var limite = DateTime.UtcNow + maximo;
        while (DateTime.UtcNow < limite)
        {
            var r = await canal.BasicGetAsync(cola, autoAck: true);
            if (r is not null) return r;
            await Task.Delay(100);
        }
        return null;
    }

    private async Task EsperarRecibidoAsync(Manejador m)
    {
        for (var i = 0; i < 100 && m.Recibidos.IsEmpty; i++) await Task.Delay(50);
        Assert.False(m.Recibidos.IsEmpty);
    }

    [RabbitMqFact]
    public async Task ConfirmarHaceAckYLaColaQuedaVacia()
    {
        var (m, id) = await IniciarAsync((_, _) => Task.FromResult(ResultadoConsumo.Confirmar));
        await EsperarRecibidoAsync(m);
        var recibido = Assert.Single(m.Recibidos);
        Assert.Equal(id, recibido.MensajeId);
        Assert.Equal("envio.evento.v1", recibido.Tipo);
        Assert.Equal("""{"x":1}""", recibido.Cuerpo);
        Assert.Equal(0, recibido.Intentos);
        await _consumidor!.DetenerAsync();
        Assert.Null(await EsperarAsync(_opciones.ColaAvisos, TimeSpan.FromMilliseconds(500)));
        Assert.Null(await EsperarAsync(_opciones.ColaMuertos, TimeSpan.FromMilliseconds(300)));
    }

    [RabbitMqFact]
    public async Task DescartarVaALaColaDeMuertos()
    {
        var (m, id) = await IniciarAsync((_, _) => Task.FromResult(ResultadoConsumo.Descartar));
        var muerto = await EsperarAsync(_opciones.ColaMuertos, TimeSpan.FromSeconds(5));
        Assert.NotNull(muerto);
        Assert.Equal(id, muerto.BasicProperties.MessageId);
        Assert.Single(m.Recibidos);
    }

    [RabbitMqFact]
    public async Task ReintentarMueveElMensajeALaColaDeEsperaConElContadorDeIntentos()
    {
        var (_, id) = await IniciarAsync((_, _) => Task.FromResult(ResultadoConsumo.Reintentar));
        var espera = await EsperarAsync(_opciones.ColaEspera("5s"), TimeSpan.FromSeconds(4));
        Assert.NotNull(espera);
        Assert.Equal(id, espera.BasicProperties.MessageId);
        Assert.Equal(DeliveryModes.Persistent, espera.BasicProperties.DeliveryMode);
        Assert.Equal(1, Convert.ToInt32(espera.BasicProperties.Headers![ConsumidorRabbitMq.EncabezadoIntentos]));
    }

    [RabbitMqFact]
    public async Task ElSegundoReintentoUsaLaEsperaDe30sYElTercerUsaLaDe2m()
    {
        var (_, id) = await IniciarAsync((_, _) => Task.FromResult(ResultadoConsumo.Reintentar), intentosPrevios: 1);
        var espera = await EsperarAsync(_opciones.ColaEspera("30s"), TimeSpan.FromSeconds(4));
        Assert.NotNull(espera);
        Assert.Equal(id, espera.BasicProperties.MessageId);
        Assert.Equal(2, Convert.ToInt32(espera.BasicProperties.Headers![ConsumidorRabbitMq.EncabezadoIntentos]));
    }

    [RabbitMqFact]
    public async Task AgotadosLosReintentosElMensajeVaALaColaDeMuertos()
    {
        var (_, id) = await IniciarAsync((_, _) => Task.FromResult(ResultadoConsumo.Reintentar), intentosPrevios: 3);
        var muerto = await EsperarAsync(_opciones.ColaMuertos, TimeSpan.FromSeconds(5));
        Assert.NotNull(muerto);
        Assert.Equal(id, muerto.BasicProperties.MessageId);
    }

    [RabbitMqFact]
    public async Task ElApagadoCancelaElMensajeEnCursoYElBrokerLoReentrega()
    {
        var cancelado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (m, id) = await IniciarAsync(async (_, ct) =>
        {
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { cancelado.SetResult(); throw; }
            return ResultadoConsumo.Confirmar;
        });
        await EsperarRecibidoAsync(m);

        await _consumidor!.DetenerAsync(); // EsperaCierre = 400 ms

        await cancelado.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var reentregado = await EsperarAsync(_opciones.ColaAvisos, TimeSpan.FromSeconds(5));
        Assert.NotNull(reentregado);
        Assert.Equal(id, reentregado.BasicProperties.MessageId);
    }
}
