using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tablero;

public sealed record CambioEnvio(Guid OperadorId, Guid EnvioId, TipoEvento Tipo, EstadoEnvio? EstadoAnterior,
    EstadoEnvio EstadoNuevo, DateTime OcurridoEn, string? Detalle);

public interface INotificadorTablero
{
    // Cada cambio va solo al tablero de su operador.
    Task PublicarAsync(IReadOnlyList<CambioEnvio> cambios, CancellationToken cancellationToken = default);
}
