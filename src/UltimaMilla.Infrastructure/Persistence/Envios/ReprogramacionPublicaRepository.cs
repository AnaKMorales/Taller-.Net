using UltimaMilla.Application.Envios.ReprogramacionPublica;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Envios;

// Carga el envío por su token público para que el destinatario lo reprograme.
// 1. Lookup exacto por token sin filtros (igual que SeguimientoPublico): solo obtiene el envío y su operador.
// 2. Abre un contexto de proceso interno de ESE operador para cargar y guardar: los filtros de tenant siguen
//    activos y el guardado pasa por los mismos interceptores (outbox, tablero e invalidación del seguimiento).
// El contexto del seguimiento público no sirve: es de solo lectura a propósito.
public sealed class ReprogramacionPublicaRepository(DbContextOptions<AppDbContext> options)
    : IReprogramacionPublicaRepository, IAsyncDisposable
{
    private AppDbContext? _db;

    public async Task<Envio?> CargarPorTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(token, "N", out var parsed)) return null;
        var normalizado = parsed.ToString("N");
        Guid envioId, operadorId;
        await using (var lookup = new AppDbContext(options))
        {
            var ambito = await lookup.Envios.IgnoreQueryFilters().Where(e => e.CodigoSeguimiento == normalizado)
                .Select(e => new { e.Id, Operador = EF.Property<Guid>(e, TenantModel.TenantId) })
                .SingleOrDefaultAsync(cancellationToken);
            if (ambito is null) return null;
            (envioId, operadorId) = (ambito.Id, ambito.Operador);
        }

        if (_db is not null) await _db.DisposeAsync();
        _db = new AppDbContext(options, TenantActual.ParaOperador(operadorId));
        return await _db.Envios.AsSplitQuery()
            .Include(e => e.CuentaComercial).ThenInclude(c => c.Operador)
            .Include(e => e.Zona).ThenInclude(z => z.Franjas)
            .Include(e => e.Programacion).ThenInclude(p => p.Franja)
            .Include(e => e.Reglas).ThenInclude(r => r.PoliticaDevolucion)
            .Include(e => e.Bultos)
            .Include(e => e.Parada)
            .Include(e => e.Devolucion)
            .Include(e => e.Intentos).ThenInclude(i => i.MotivoNoEntrega)
            .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Motivo)
            .SingleOrDefaultAsync(e => e.Id == envioId, cancellationToken);
    }

    public async Task GuardarAsync(Envio envio, CancellationToken cancellationToken = default)
    {
        if (_db is null) throw new InvalidOperationException("Primero hay que cargar el envío.");
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null) await _db.DisposeAsync();
    }
}
