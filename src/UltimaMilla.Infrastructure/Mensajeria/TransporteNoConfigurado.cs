using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Infrastructure.Mensajeria;

// RabbitMQ no está configurado en este host (por ejemplo, desarrollo sin broker): publicar falla siempre.
public sealed class TransporteNoConfigurado : ITransporteMensajes
{
    public Task PublicarConfirmadoAsync(MensajeParaPublicar mensaje, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("RabbitMQ no está configurado en este host.");
}
