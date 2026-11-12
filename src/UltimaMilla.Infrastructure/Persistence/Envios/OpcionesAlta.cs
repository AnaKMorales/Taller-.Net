using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Envios;

// Zonas, franjas y plazos que puede elegir el comercio autenticado. Las zonas y las reglas son catálogos del
// operador: el filtro de tenant ya limita la lectura al operador del usuario.
public sealed class OpcionesAlta(AppDbContext db, TimeProvider reloj) : IOpcionesAlta
{
    public async Task<OpcionesAltaDto?> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        if (db.ContextoTenant is not { Origen: OrigenTenant.UsuarioAutenticado, CuentaComercialId: Guid cuentaId } tenant)
            throw new AislamientoOperadorException();

        var ahora = reloj.GetUtcNow().UtcDateTime;
        // Mismo criterio que la configuración versionada: la versión vigente es la última que ya empezó a regir.
        var reglas = await db.Set<ReglasOperativas>().AsNoTracking().Include(r => r.PlazosServicio)
            .Where(r => r.VigenteDesde <= ahora)
            .OrderByDescending(r => r.VigenteDesde).ThenByDescending(r => r.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (reglas is null) return null;

        var zonas = await db.Set<ZonaCobertura>().AsNoTracking().Include(z => z.Franjas)
            .OrderBy(z => z.Nombre).ToListAsync(cancellationToken);
        return new OpcionesAltaDto(tenant.OperadorId, cuentaId, reglas.Id,
            zonas.Select(z => new ZonaAltaDto(z.Id, z.Nombre, z.CodigoPostal,
                z.Franjas.OrderBy(f => f.Desde).Select(f => new FranjaAltaDto(f.Id, f.Nombre, f.Desde, f.Hasta)).ToList()))
                .ToList(),
            reglas.PlazosServicio.OrderBy(p => p.ModalidadServicio)
                .Select(p => new PlazoAltaDto(p.ModalidadServicio, p.PlazoComprometidoHoras, p.HoraCorte)).ToList());
    }
}
