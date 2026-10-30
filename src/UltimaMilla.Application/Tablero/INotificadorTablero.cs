using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tablero;

// Un cambio de estado de un envío, tal como lo ve el tablero en vivo del backoffice.
public sealed record CambioEnvio(Guid OperadorId, Guid EnvioId, TipoEvento Tipo, EstadoEnvio? EstadoAnterior,
    EstadoEnvio EstadoNuevo, DateTime OcurridoEn, string? Detalle);

public interface INotificadorTablero
{
    // Avisa a los tableros abiertos de cada operador. Nunca debe enviar un cambio a otro operador.
    Task PublicarAsync(IReadOnlyList<CambioEnvio> cambios, CancellationToken cancellationToken = default);
}
