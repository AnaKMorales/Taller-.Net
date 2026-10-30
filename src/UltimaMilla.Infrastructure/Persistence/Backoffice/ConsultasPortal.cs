using UltimaMilla.Application.Portal;

namespace UltimaMilla.Infrastructure.Persistence.Backoffice;

public sealed class ConsultasPortal(AppDbContext db) : IConsultasPortal
{
    public async Task<IReadOnlyList<EnvioPortalDto>> EnviosAsync(CancellationToken cancellationToken = default)
    {
        if (!db.CuentaRestringida) return [];
        return await db.Envios.AsNoTracking()
            .OrderByDescending(e => e.FechaAdmision)
            .Select(e => new EnvioPortalDto(e.Id, e.CodigoSeguimiento, e.Destinatario.Nombre, e.Estado, e.FechaAdmision))
            .ToListAsync(cancellationToken);
    }
}
