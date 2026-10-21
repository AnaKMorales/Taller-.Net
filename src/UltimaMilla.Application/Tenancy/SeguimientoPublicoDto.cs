using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tenancy;

public sealed record SeguimientoPublicoDto(EstadoEnvio Estado, DateTime FechaComprometida,
    DateTime VentanaDesde, DateTime VentanaHasta);

public interface ISeguimientoPublico
{
    Task<SeguimientoPublicoDto?> ConsultarAsync(string token, CancellationToken cancellationToken = default);
}
