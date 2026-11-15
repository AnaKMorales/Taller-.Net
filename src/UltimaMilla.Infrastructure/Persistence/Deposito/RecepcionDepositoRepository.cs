using System.Data;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Deposito;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Deposito;

public sealed class RecepcionDepositoRepository(DbContextOptions<AppDbContext> options, ITenantActual actual)
    : IRecepcionDepositoRepository
{
    public async Task<IOperacionRecepcionDeposito> AbrirAsync(Guid envioId, CancellationToken ct)
    {
        if (actual.Contexto is not { Origen: OrigenTenant.UsuarioAutenticado, UsuarioId: Guid usuarioId,
                Rol: RolUsuario.Administrador or RolUsuario.Despachador or RolUsuario.OperarioDeposito } tenant)
            throw new AislamientoOperadorException();
        var db = new AppDbContext(options, new TenantActual(tenant));
        try
        {
            await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var actor = await db.Set<Usuario>().Include(u => u.Operador).SingleOrDefaultAsync(u =>
                u.Id == usuarioId && u.Activo && u.Rol == tenant.Rol, ct) ?? throw new AislamientoOperadorException();
            var envio = await db.Envios.AsSplitQuery()
                .Include(e => e.CuentaComercial).ThenInclude(c => c.Operador)
                .Include(e => e.Bultos).ThenInclude(b => b.Escaneos)
                .Include(e => e.Discrepancias)
                .SingleOrDefaultAsync(e => e.Id == envioId, ct) ?? throw new AislamientoOperadorException();
            await snapshot.CommitAsync(ct);
            return new Operacion(db, envio, actor);
        }
        catch { await db.DisposeAsync(); throw; }
    }

    private sealed class Operacion(AppDbContext db, Envio envio, Usuario actor) : IOperacionRecepcionDeposito
    {
        public Envio Envio => envio;
        public Usuario Responsable => actor;
        public Guid Version => db.Entry(envio).Property<Guid>("RowVersion").CurrentValue;
        public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
