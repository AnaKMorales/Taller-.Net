using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

public sealed class SeguimientoPublico(DbContextOptions<AppDbContext> options) : ISeguimientoPublico
{
    public async Task<SeguimientoPublicoDto?> ConsultarAsync(string token, CancellationToken cancellationToken = default)
    {
        // CodigoSeguimiento ya se genera con un GUID aleatorio independiente del Id interno.
        if (!Guid.TryParseExact(token, "N", out var parsed)) return null;
        var normalized = parsed.ToString("N");
        await using var lookup = new AppDbContext(options);
        // Único acceso público sin contexto: lookup exacto por token; nunca devuelve entidades.
        var scope = await lookup.Envios.IgnoreQueryFilters().Where(e => e.CodigoSeguimiento == normalized)
            .Select(e => new { e.Id, Operador = EF.Property<Guid>(e, TenantModel.TenantId), Cuenta = EF.Property<Guid>(e, "CuentaComercialKey") })
            .SingleOrDefaultAsync(cancellationToken);
        if (scope is null) return null;
        var context = TenantContext.ParaSeguimiento(scope.Operador, scope.Cuenta, scope.Id);
        await using var db = new AppDbContext(options, new TenantActual(context));
        var result = await db.Envios.Where(e => e.Id == scope.Id).Select(e => new
        {
            e.Estado, e.FechaComprometida, e.Programacion.InicioLocal, e.Programacion.FinLocal
        }).SingleOrDefaultAsync(cancellationToken);
        return result is null ? null : new(result.Estado, result.FechaComprometida, result.InicioLocal.UtcDateTime, result.FinLocal.UtcDateTime);
    }
}
