using System.Data;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.RegistrarIntento;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Intentos;

public sealed class RegistroIntentoRepository(DbContextOptions<AppDbContext> options, ITenantActual actual)
    : IRegistroIntentoRepository
{
    public async Task<IOperacionRegistroIntento> AbrirAsync(Guid envioId, Guid idOperacion, CancellationToken ct)
    {
        var tenant = actual.Contexto;
        if (tenant is not { Origen: OrigenTenant.UsuarioAutenticado, Rol: RolUsuario.Repartidor, UsuarioId: not null })
            throw new AislamientoOperadorException();
        var db = new AppDbContext(options, new TenantActual(tenant));
        try
        {
            // Los Includes divididos deben observar la misma versión del grafo. La transacción
            // de lectura termina antes de operar: SaveChanges mantiene su concurrencia optimista.
            await using var lectura = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var responsable = await db.Set<Repartidor>().Include(r => r.Operador)
                .SingleOrDefaultAsync(r => r.Id == tenant.UsuarioId && r.Activo && r.Rol == RolUsuario.Repartidor, ct)
                ?? throw new AislamientoOperadorException();
            var envio = await Grafo(db).SingleOrDefaultAsync(e => e.Id == envioId &&
                ((e.Parada != null && e.Parada.Ruta.Repartidor.Id == responsable.Id) ||
                 e.Intentos.Any(i => i.IdOperacion == idOperacion && i.Parada.Ruta.Repartidor.Id == responsable.Id)), ct)
                ?? throw new AislamientoOperadorException();
            await lectura.CommitAsync(ct);
            return new Operacion(db, envio, responsable);
        }
        catch { await db.DisposeAsync(); throw; }
    }

    private static IQueryable<Envio> Grafo(AppDbContext db) => db.Envios.AsSplitQuery()
        .Include(e => e.CuentaComercial).ThenInclude(c => c.Operador)
        .Include(e => e.Bultos)
        .Include(e => e.Reglas).ThenInclude(r => r.MotivosNoEntrega)
        .Include(e => e.Reglas).ThenInclude(r => r.PoliticaDevolucion)
        .Include(e => e.Parada).ThenInclude(p => p!.Ruta).ThenInclude(r => r.Repartidor)
        .Include(e => e.Parada).ThenInclude(p => p!.Bultos).ThenInclude(b => b.Bulto)
        .Include(e => e.Parada).ThenInclude(p => p!.Recargo)
        .Include(e => e.Recargos).ThenInclude(r => r.Parada)
        .Include(e => e.Eventos).ThenInclude(v => v.Responsable)
        .Include(e => e.Intentos).ThenInclude(i => i.Pruebas)
        .Include(e => e.Intentos).ThenInclude(i => i.Parada).ThenInclude(p => p.Ruta).ThenInclude(r => r.Repartidor)
        .Include(e => e.Intentos).ThenInclude(i => i.Parada).ThenInclude(p => p.Bultos).ThenInclude(b => b.Bulto)
        .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Bulto)
        .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Motivo)
        .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Evidencias).ThenInclude(p => p.Prueba);

    private sealed class Operacion(AppDbContext db, Envio envio, Repartidor responsable) : IOperacionRegistroIntento
    {
        public Envio Envio => envio;
        public Repartidor Responsable => responsable;
        public Guid VersionActual => db.Entry(envio).Property<Guid>("RowVersion").CurrentValue;

        public async Task GuardarAsync(CancellationToken ct)
        {
            if (!await db.Set<Repartidor>().AnyAsync(r => r.Id == responsable.Id && r.Activo && r.Rol == RolUsuario.Repartidor, ct))
                throw new AislamientoOperadorException();
            await db.SaveChangesAsync(ct);
        }

        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
