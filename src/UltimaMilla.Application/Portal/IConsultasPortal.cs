using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Portal;

public sealed record EnvioPortalDto(Guid EnvioId, string CodigoSeguimiento, string Destinatario, EstadoEnvio Estado,
    DateTime FechaAdmision);

// Consultas del portal del comercio. Devuelven solo envíos de la cuenta comercial del usuario.
public interface IConsultasPortal
{
    Task<IReadOnlyList<EnvioPortalDto>> EnviosAsync(CancellationToken cancellationToken = default);
}
