using RabbitMQ.Client;
using System.Text;
using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Infrastructure.Mensajeria;

// Publica en el exchange topic con routing key = Tipo, mensajes persistentes y publisher confirms.
// PublicarConfirmadoAsync termina solo cuando el broker confirma; un nack, un mensaje sin ruta
// (mandatory + basic.return) o una cancelación lo hacen fallar, y el outbox deja la fila pendiente.
public sealed class TransporteRabbitMq(ConexionRabbitMq conexion, OpcionesRabbitMq opciones)
    : ITransporteMensajes, IAsyncDisposable
{
    private readonly SemaphoreSlim _candado = new(1, 1);
    private IChannel? _canal;

    public async Task PublicarConfirmadoAsync(MensajeParaPublicar mensaje, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        if (string.IsNullOrWhiteSpace(mensaje.Tipo)) throw new ArgumentException("El mensaje no tiene Tipo.", nameof(mensaje));

        var canal = await ObtenerCanalAsync(cancellationToken);
        var propiedades = new BasicProperties
        {
            MessageId = mensaje.MensajeId.ToString(),
            Type = mensaje.Tipo,
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(DateTime.SpecifyKind(mensaje.CreadoEn, DateTimeKind.Utc)).ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?> { ["operador-id"] = mensaje.OperadorId.ToString() },
        };
        // Con confirmaciones y seguimiento activos, la tarea espera el ack del broker y lanza
        // PublishException ante nack o devolución.
        await canal.BasicPublishAsync(opciones.Exchange, mensaje.Tipo, mandatory: true, propiedades,
            Encoding.UTF8.GetBytes(mensaje.Contenido), cancellationToken);
    }

    private async Task<IChannel> ObtenerCanalAsync(CancellationToken cancellationToken)
    {
        if (_canal is { IsOpen: true }) return _canal;
        await _candado.WaitAsync(cancellationToken);
        try
        {
            if (_canal is { IsOpen: true }) return _canal;
            if (_canal is not null) await _canal.DisposeAsync();
            var amqp = await conexion.ObtenerAsync(cancellationToken);
            _canal = await amqp.CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                cancellationToken);
            return _canal;
        }
        finally { _candado.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_canal is not null) await _canal.DisposeAsync();
        _candado.Dispose();
    }
}
