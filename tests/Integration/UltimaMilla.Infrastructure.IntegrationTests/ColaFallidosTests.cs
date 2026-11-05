using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Infrastructure.Mensajeria;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed class ColaFallidosTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly OpcionesRabbitMq _opciones = new()
    {
        Host = RabbitMqFactAttribute.Variable("HOST") ?? "127.0.0.1",
        Puerto = int.TryParse(RabbitMqFactAttribute.Variable("PUERTO"), out var p) ? p : 55672,
        Usuario = RabbitMqFactAttribute.Variable("USUARIO") ?? "ultimamilla",
        Clave = RabbitMqFactAttribute.Variable("CLAVE") ?? "",
    };
    private readonly ConexionRabbitMq _conexion;
    private readonly ColaFallidosRabbitMq _cola;

    public ColaFallidosTests()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        _opciones.Exchange = $"test.{sufijo}.eventos";
        _opciones.ColaAvisos = $"test.{sufijo}.avisos";
        _conexion = new ConexionRabbitMq(_opciones);
        _cola = new ColaFallidosRabbitMq(_conexion, _opciones);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_opciones.Clave)) return;
        var amqp = await _conexion.ObtenerAsync();
        await using var canal = await amqp.CreateChannelAsync();
        await canal.ExchangeDeleteAsync(_opciones.Exchange);
        foreach (var cola in new[] { _opciones.ColaAvisos, _opciones.ColaMuertos }
                     .Concat(_opciones.Esperas.Select(e => _opciones.ColaEspera(e.Sufijo))))
            await canal.QueueDeleteAsync(cola);
        await _conexion.DisposeAsync();
    }

    // Deja un mensaje directamente en la DLQ, como lo hace el broker tras un nack sin reencolar.
    private async Task<string> PonerEnDlqAsync(Guid operador, string? cuerpo = null, int intentos = 3)
    {
        var id = Guid.NewGuid();
        cuerpo ??= JsonSerializer.Serialize(new EventoEnvioV1(id, 1, operador, Guid.NewGuid(), Guid.NewGuid(), id,
            "Validar", "Sistema", "Admitido", "EnDeposito", DateTime.UtcNow, null), Json);
        var amqp = await _conexion.ObtenerAsync();
        var canal = await amqp.CreateChannelAsync(new CreateChannelOptions(true, true));
        await canal.BasicPublishAsync("", _opciones.ColaMuertos, mandatory: true, new BasicProperties
        {
            MessageId = id.ToString(), Type = "envio.evento.v1", DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>
            {
                [ConsumidorRabbitMq.EncabezadoIntentos] = intentos,
                ["x-death"] = new List<object> { new Dictionary<string, object> { ["queue"] = "q" } },
                ["x-first-death-reason"] = "rejected",
                ["operador-id"] = operador.ToString(),
            },
        }, Encoding.UTF8.GetBytes(cuerpo));
        await canal.CloseAsync(); await canal.DisposeAsync();
        return id.ToString();
    }

    private async Task<uint> ContarAsync(string cola)
    {
        var amqp = await _conexion.ObtenerAsync();
        var canal = await amqp.CreateChannelAsync();
        var cantidad = (await canal.QueueDeclarePassiveAsync(cola)).MessageCount;
        await canal.CloseAsync(); await canal.DisposeAsync();
        return cantidad;
    }

    private async Task<BasicGetResult?> TomarAsync(string cola)
    {
        var amqp = await _conexion.ObtenerAsync();
        var canal = await amqp.CreateChannelAsync();
        try
        {
            for (var i = 0; i < 40; i++)
            {
                var r = await canal.BasicGetAsync(cola, autoAck: true);
                if (r is not null) return r;
                await Task.Delay(100);
            }
            return null;
        }
        finally { await canal.CloseAsync(); await canal.DisposeAsync(); }
    }

    [RabbitMqFact]
    public async Task ListaSoloLosMensajesDelOperadorYLosDejaEnLaCola()
    {
        await new TopologiaRabbitMq(_conexion, _opciones).DeclararAsync();
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        var deA1 = await PonerEnDlqAsync(a); var deA2 = await PonerEnDlqAsync(a);
        var deB = await PonerEnDlqAsync(b);
        await PonerEnDlqAsync(a, cuerpo: "esto no es json");            // sin operador: no se muestra a nadie
        Assert.Equal(4u, await ContarAsync(_opciones.ColaMuertos));

        var deOperadorA = await _cola.ListarAsync(a);
        var deOperadorB = await _cola.ListarAsync(b);

        Assert.Equal(new[] { deA1, deA2 }.Order(), deOperadorA.Select(m => m.MensajeId).Order());
        Assert.Equal([deB], deOperadorB.Select(m => m.MensajeId));
        Assert.All(deOperadorA, m => Assert.Equal(3, m.Intentos));
        Assert.Empty(await _cola.ListarAsync(Guid.NewGuid()));
        Assert.Equal(4u, await ContarAsync(_opciones.ColaMuertos)); // listar no consume
    }

    [RabbitMqFact]
    public async Task ReintentarDevuelveElMensajeAAvisosConElContadorEnCero()
    {
        await new TopologiaRabbitMq(_conexion, _opciones).DeclararAsync();
        var operador = Guid.NewGuid();
        var elegido = await PonerEnDlqAsync(operador);
        var otro = await PonerEnDlqAsync(operador);

        Assert.True(await _cola.ReintentarAsync(operador, elegido));

        var enAvisos = await TomarAsync(_opciones.ColaAvisos);
        Assert.NotNull(enAvisos);
        Assert.Equal(elegido, enAvisos.BasicProperties.MessageId);
        Assert.Equal(DeliveryModes.Persistent, enAvisos.BasicProperties.DeliveryMode);
        Assert.Equal(0, PoliticaReintentos.LeerIntentos(enAvisos.BasicProperties.Headers));
        Assert.DoesNotContain("x-death", enAvisos.BasicProperties.Headers ?? new Dictionary<string, object?>());
        // En la DLQ queda solo el otro.
        Assert.Equal([otro], (await _cola.ListarAsync(operador)).Select(m => m.MensajeId));
        Assert.Equal(1u, await ContarAsync(_opciones.ColaMuertos));
    }

    [RabbitMqFact]
    public async Task NoSePuedeReintentarElMensajeDeOtroOperadorNiUnoInexistente()
    {
        await new TopologiaRabbitMq(_conexion, _opciones).DeclararAsync();
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        var deB = await PonerEnDlqAsync(b);

        Assert.False(await _cola.ReintentarAsync(a, deB));                         // ajeno
        Assert.False(await _cola.ReintentarAsync(a, Guid.NewGuid().ToString()));   // inexistente

        Assert.Equal(1u, await ContarAsync(_opciones.ColaMuertos));
        Assert.Equal(0u, await ContarAsync(_opciones.ColaAvisos));
        Assert.Equal([deB], (await _cola.ListarAsync(b)).Select(m => m.MensajeId));
    }
}
