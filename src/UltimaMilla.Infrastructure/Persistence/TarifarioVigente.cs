using UltimaMilla.Application.Tarifas;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence;

// Lectura sin caché. La versión cacheada (Cache/TarifarioVigenteCacheado) envuelve a esta.
public sealed class TarifarioVigente(DbContextOptions<AppDbContext> options, TimeProvider reloj) : ITarifarioVigente
{
    public async Task<TarifarioDto?> ObtenerAsync(Guid operadorId, CancellationToken cancellationToken = default)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        // El contexto queda acotado al operador: el filtro de tenant impide leer cuadros de otro.
        await using var db = new AppDbContext(options, TenantActual.ParaOperador(operadorId));
        var cuadros = db.Set<CuadroTarifario>().AsNoTracking();

        var vigente = await cuadros
            .Where(c => c.VigenteDesde <= ahora)
            .OrderByDescending(c => c.VigenteDesde).ThenByDescending(c => c.Version)
            .Select(c => new TarifarioDto(c.Id, c.Version, c.VigenteDesde, null,
                c.Lineas.Select(l => new LineaTarifaDto(l.Zona.Id, l.ModalidadServicio, l.PesoDesde, l.PesoHasta,
                    l.VolumenHasta, l.Precio,
                    l.Ajustes.Select(a => new AjusteTarifaDto(a.Tipo, a.ValorPorcentaje)).ToList())).ToList()))
            .FirstOrDefaultAsync(cancellationToken);
        if (vigente is null) return null;

        var proxima = await cuadros.Where(c => c.VigenteDesde > ahora)
            .MinAsync(c => (DateTime?)c.VigenteDesde, cancellationToken);
        return vigente with { ProximaVigencia = proxima };
    }
}
