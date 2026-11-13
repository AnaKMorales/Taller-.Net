using System.Data;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Despacho;

public sealed class DespachoRutaRepository(DbContextOptions<AppDbContext> options, ITenantActual actual)
    : IDespachoRutaRepository, IRegistroCargaRutaRepository
{
    public Task<IOperacionDespachoRuta> AbrirAsync(Guid rutaId, CancellationToken ct) => Abrir(rutaId, false, ct);
    public Task<IOperacionDespachoRuta> AbrirCargaAsync(Guid rutaId, CancellationToken ct) => Abrir(rutaId, true, ct);

    private async Task<IOperacionDespachoRuta> Abrir(Guid rutaId, bool carga, CancellationToken ct)
    {
        if (actual.Contexto is not { Origen: OrigenTenant.UsuarioAutenticado, UsuarioId: Guid usuarioId,
                Rol: RolUsuario.Administrador or RolUsuario.Despachador or RolUsuario.OperarioDeposito or RolUsuario.Repartidor } tenant ||
                !carga && tenant.Rol is not (RolUsuario.Administrador or RolUsuario.Despachador))
            throw new AislamientoOperadorException();
        var db = new AppDbContext(options, new TenantActual(tenant));
        try
        {
            await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var actor = await db.Set<Usuario>().Include(u => u.Operador).SingleOrDefaultAsync(u =>
                u.Id == usuarioId && u.Activo && u.Rol == tenant.Rol, ct) ?? throw new AislamientoOperadorException();
            var ruta = await db.Set<Ruta>().AsSplitQuery()
                .Include(r => r.Repartidor).ThenInclude(r => r.Operador)
                .Include(r => r.Vehiculo).ThenInclude(v => v.Operador)
                .Include(r => r.Rendicion)
                .Include(r => r.Escaneos)
                .Include(r => r.Paradas).ThenInclude(p => p.Bultos).ThenInclude(b => b.Bulto)
                .Include(r => r.Paradas).ThenInclude(p => p.Recargo)
                .Include(r => r.Paradas).ThenInclude(p => p.Envio).ThenInclude(e => e.CuentaComercial).ThenInclude(c => c.Operador)
                .Include(r => r.Paradas).ThenInclude(p => p.Envio).ThenInclude(e => e.Bultos)
                .Include(r => r.Paradas).ThenInclude(p => p.Envio).ThenInclude(e => e.Eventos)
                .Include(r => r.Paradas).ThenInclude(p => p.Envio).ThenInclude(e => e.Parada)
                .Include(r => r.Paradas).ThenInclude(p => p.Envio).ThenInclude(e => e.Reglas).ThenInclude(r => r.PoliticaDevolucion)
                .SingleOrDefaultAsync(r => r.Id == rutaId, ct) ?? throw new AislamientoOperadorException();
            if (tenant.Rol == RolUsuario.Repartidor && ruta.RepartidorId != usuarioId)
                throw new AislamientoOperadorException();
            await snapshot.CommitAsync(ct);
            return new Operacion(db, ruta, actor);
        }
        catch { await db.DisposeAsync(); throw; }
    }

    private sealed class Operacion(AppDbContext db, Ruta ruta, Usuario actor) : IOperacionDespachoRuta
    {
        public Ruta Ruta => ruta;
        public Usuario Responsable => actor;
        public Guid Version => db.Entry(ruta).Property<Guid>("RowVersion").CurrentValue;
        public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
