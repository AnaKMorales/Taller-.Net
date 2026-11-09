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

        // el contexto público no puede leer bultos ni operadores, y así tampoco hace falta abrirlos.
        var scope = await lookup.Envios.IgnoreQueryFilters().Where(e => e.CodigoSeguimiento == normalized)
            .Select(e => new
            {
                e.Id,
                Operador = EF.Property<Guid>(e, TenantModel.TenantId),
                Cuenta = EF.Property<Guid>(e, "CuentaComercialKey"),
                NombreOperador = e.CuentaComercial.Operador.RazonSocial,
                Total = e.Bultos.Count(),
                Entregados = e.Bultos.Count(b => b.Estado == EstadoBulto.Entregado),
                EnDevolucion = e.Bultos.Count(b => b.Estado == EstadoBulto.EnDevolucion || b.Estado == EstadoBulto.Devuelto)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (scope is null) return null;

        var context = TenantContext.ParaSeguimiento(scope.Operador, scope.Cuenta, scope.Id);
        await using var db = new AppDbContext(options, new TenantActual(context));
        var result = await db.Envios.Where(e => e.Id == scope.Id).Select(e => new
        {
            e.Estado, e.FechaComprometida, e.Programacion.InicioLocal, e.Programacion.FinLocal
        }).SingleOrDefaultAsync(cancellationToken);
        if (result is null) return null;

        // Sin Detalle, Responsable ni Posicion: pueden contener datos personales o internos.
        var historial = await db.Set<EventoDeEnvio>()
            .Where(ev => EF.Property<Guid>(ev, "EnvioKey") == scope.Id)
            .OrderBy(ev => ev.OcurridoEn)
            .Select(ev => new HitoSeguimientoDto(ev.Tipo, ev.EstadoNuevo, ev.OcurridoEn))
            .ToListAsync(cancellationToken);

        return new(scope.NombreOperador, result.Estado, result.FechaComprometida, result.InicioLocal, result.FinLocal,
            new AvanceBultosDto(scope.Total, scope.Entregados, scope.EnDevolucion), historial);
    }
}
