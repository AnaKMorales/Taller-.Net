using System.Data;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.Extravio;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Envios;

public sealed class ExtravioRepository(DbContextOptions<AppDbContext> options, ITenantActual actual) : IExtravioRepository
{
    public async Task<IOperacionExtravio> AbrirAsync(Guid envioId, CancellationToken ct)
    {
        if (actual.Contexto is not { Origen: OrigenTenant.UsuarioAutenticado, UsuarioId: Guid usuarioId,
            Rol: RolUsuario.Administrador or RolUsuario.Despachador } tenant)
            throw new AislamientoOperadorException();
        var db = new AppDbContext(options, new TenantActual(tenant));
        try
        {
            await using var lectura = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var actor = await db.Set<Usuario>().Include(u => u.Operador).SingleOrDefaultAsync(u =>
                u.Id == usuarioId && u.Activo && u.Rol == tenant.Rol, ct) ?? throw new AislamientoOperadorException();
            var envio = await db.Envios.AsSplitQuery()
                .Include(e => e.CuentaComercial).ThenInclude(c => c.Operador)
                .Include(e => e.Bultos).Include(e => e.Eventos)
                .Include(e => e.Parada).ThenInclude(p => p!.Bultos).ThenInclude(b => b.Bulto)
                .Include(e => e.Parada).ThenInclude(p => p!.Ruta)
                .SingleOrDefaultAsync(e => e.Id == envioId, ct) ?? throw new AislamientoOperadorException();
            await lectura.CommitAsync(ct);
            return new Operacion(db, envio, actor, envio.Parada?.Ruta);
        }
        catch { await db.DisposeAsync(); throw; }
    }

    private sealed class Operacion(AppDbContext db, Envio envio, Usuario actor, Ruta? ruta) : IOperacionExtravio
    {
        public Envio Envio => envio;
        public Ruta? Ruta => ruta;
        public Usuario Responsable => actor;
        public Guid VersionEnvio => db.Entry(envio).Property<Guid>("RowVersion").CurrentValue;
        public Guid? VersionRuta => ruta is null ? null : db.Entry(ruta).Property<Guid>("RowVersion").CurrentValue;
        public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
