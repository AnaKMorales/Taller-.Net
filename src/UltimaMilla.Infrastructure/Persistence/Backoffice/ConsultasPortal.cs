using UltimaMilla.Application.Portal;

namespace UltimaMilla.Infrastructure.Persistence.Backoffice;

// El filtro de cuenta del AppDbContext (perfil Comercio) ya limita los envíos a la cuenta del usuario.
public sealed class ConsultasPortal(AppDbContext db) : IConsultasPortal
{
    public async Task<IReadOnlyList<EnvioPortalDto>> EnviosAsync(CancellationToken cancellationToken = default)
    {
        // Sin cuenta comercial activa no hay envíos que mostrar en el portal.
        if (!db.CuentaRestringida) return [];
        return await db.Envios.AsNoTracking()
            .OrderByDescending(e => e.FechaAdmision)
            .Select(e => new EnvioPortalDto(e.Id, e.CodigoSeguimiento, e.Destinatario.Nombre, e.Estado, e.FechaAdmision))
            .ToListAsync(cancellationToken);
    }
}
