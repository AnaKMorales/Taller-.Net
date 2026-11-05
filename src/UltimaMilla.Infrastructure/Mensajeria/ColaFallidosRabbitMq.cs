using System.Text;
using RabbitMQ.Client;
using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Infrastructure.Mensajeria;

// Lee avisos.dlq sin consumirla: toma mensajes con BasicGet sin ack y al final los devuelve (nack + requeue).
// Pensado para una pantalla de administración con pocos mensajes, no para volúmenes grandes.
public sealed class ColaFallidosRabbitMq(ConexionRabbitMq conexion, OpcionesRabbitMq opciones) : IColaFallidos
{
    // Tope de mensajes inspeccionados por consulta, para no retener la cola entera sin ack.
    private const int MaximoInspeccionados = 500;
    private static readonly string[] EncabezadosDelBroker = ["x-death", "x-first-death-exchange", "x-first-death-queue",
        "x-first-death-reason", "x-last-death-exchange", "x-last-death-queue", "x-last-death-reason", ConsumidorRabbitMq.EncabezadoIntentos];

    public bool Configurada => true;

    public async Task<IReadOnlyList<MensajeFallido>> ListarAsync(Guid operadorId, int maximo = 100, CancellationToken cancellationToken = default)
    {
        if (operadorId == Guid.Empty) throw new ArgumentException("El operador es obligatorio.", nameof(operadorId));
        ArgumentOutOfRangeException.ThrowIfLessThan(maximo, 1);
        var amqp = await conexion.ObtenerAsync(cancellationToken);
        var canal = await amqp.CreateChannelAsync(cancellationToken: cancellationToken);
        var propios = new List<MensajeFallido>();
        ulong ultimo = 0;
        try
        {
            for (var i = 0; i < MaximoInspeccionados && propios.Count < maximo; i++)
            {
                var r = await canal.BasicGetAsync(opciones.ColaMuertos, autoAck: false, cancellationToken);
                if (r is null) break;
                ultimo = r.DeliveryTag;
                if (Convertir(r) is { } leido && leido.OperadorId == operadorId) propios.Add(leido.Mensaje);
            }
        }
        finally
        {
            // Todo lo leído vuelve a la cola, también si algo falló o se canceló.
            if (ultimo > 0) await canal.BasicNackAsync(ultimo, multiple: true, requeue: true, CancellationToken.None);
            await CerrarAsync(canal);
        }
        return propios;
    }

    public async Task<bool> ReintentarAsync(Guid operadorId, string mensajeId, CancellationToken cancellationToken = default)
    {
        if (operadorId == Guid.Empty) throw new ArgumentException("El operador es obligatorio.", nameof(operadorId));
        ArgumentException.ThrowIfNullOrWhiteSpace(mensajeId);
        var amqp = await conexion.ObtenerAsync(cancellationToken);
        var lectura = await amqp.CreateChannelAsync(cancellationToken: cancellationToken);
        var publicacion = await amqp.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), cancellationToken);
        ulong ultimo = 0;
        ulong? elegido = null;
        try
        {
            for (var i = 0; i < MaximoInspeccionados; i++)
            {
                var r = await lectura.BasicGetAsync(opciones.ColaMuertos, autoAck: false, cancellationToken);
                if (r is null) break;
                ultimo = r.DeliveryTag;
                if (r.BasicProperties.MessageId != mensajeId || Convertir(r) is not { } leido || leido.OperadorId != operadorId)
                    continue;

                // Contador en cero y sin rastros de muerte previa: vuelve a empezar la secuencia de esperas.
                var encabezados = (r.BasicProperties.Headers ?? new Dictionary<string, object?>())
                    .Where(h => !EncabezadosDelBroker.Contains(h.Key)).ToDictionary(h => h.Key, h => h.Value);
                var propiedades = new BasicProperties
                {
                    MessageId = r.BasicProperties.MessageId,
                    Type = r.BasicProperties.Type,
                    ContentType = r.BasicProperties.ContentType,
                    ContentEncoding = r.BasicProperties.ContentEncoding,
                    Timestamp = r.BasicProperties.Timestamp,
                    DeliveryMode = DeliveryModes.Persistent,
                    Headers = encabezados,
                };
                // Exchange por defecto: la routing key es el nombre de la cola. Con confirmaciones, falla si no se confirma.
                await publicacion.BasicPublishAsync("", opciones.ColaAvisos, mandatory: true, propiedades, r.Body, cancellationToken);
                elegido = r.DeliveryTag;
                break;
            }
        }
        finally
        {
            // Primero se confirma el elegido (ya está republicado); el resto vuelve a la DLQ.
            if (elegido is { } tag) await lectura.BasicAckAsync(tag, multiple: false, CancellationToken.None);
            if (ultimo > 0) await lectura.BasicNackAsync(ultimo, multiple: true, requeue: true, CancellationToken.None);
            await CerrarAsync(publicacion);
            await CerrarAsync(lectura);
        }
        return elegido is not null;
    }

    // CloseAsync espera la confirmación del broker; liberar el canal sin esperar deja que una respuesta tardía
    // llegue cuando el número de canal ya lo reutiliza otro.
    private static async Task CerrarAsync(IChannel canal)
    {
        try { if (canal.IsOpen) await canal.CloseAsync(); }
        finally { await canal.DisposeAsync(); }
    }

    private static (MensajeFallido Mensaje, Guid OperadorId)? Convertir(BasicGetResult r)
    {
        var p = r.BasicProperties;
        DateTime? fallidoEn = null;
        if (p.Headers is not null && p.Headers.TryGetValue("x-first-death-reason", out _) &&
            p.Headers.TryGetValue("x-death", out var muertes) && muertes is IList<object> lista && lista.Count > 0 &&
            lista[0] is IDictionary<string, object> primera && primera.TryGetValue("time", out var tiempo) && tiempo is AmqpTimestamp t)
            fallidoEn = DateTimeOffset.FromUnixTimeSeconds(t.UnixTime).UtcDateTime;
        return LectorFallidos.Leer(p.MessageId, p.Type, Encoding.UTF8.GetString(r.Body.Span),
            PoliticaReintentos.LeerIntentos(p.Headers), fallidoEn);
    }
}

// RabbitMQ no está configurado en este host (por ejemplo, desarrollo sin broker): la pantalla lo informa.
public sealed class ColaFallidosNoConfigurada : IColaFallidos
{
    public bool Configurada => false;
    public Task<IReadOnlyList<MensajeFallido>> ListarAsync(Guid operadorId, int maximo = 100, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MensajeFallido>>([]);
    public Task<bool> ReintentarAsync(Guid operadorId, string mensajeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
