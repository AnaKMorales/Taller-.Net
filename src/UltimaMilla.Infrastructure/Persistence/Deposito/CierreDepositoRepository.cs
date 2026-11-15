using System.Data;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Deposito;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Deposito;

public sealed class CierreDepositoRepository(DbContextOptions<AppDbContext> options, ITenantActual actual)
    : IDevolucionDepositoRepository, IRendicionRutaRepository
{
    public async Task<IReadOnlyList<DevolucionAbiertaDto>> ListarAbiertasAsync(CancellationToken ct)
    {
        await using var db = Abrir();
        await Responsable(db, ct);
        return await db.Envios.AsNoTracking().Where(e => e.Devolucion != null && e.Devolucion.Estado == EstadoDevolucion.EnProceso)
            .OrderBy(e => e.Devolucion!.PlazoLimite).ThenBy(e => e.Id)
            .Select(e => new DevolucionAbiertaDto(e.Id, e.CodigoSeguimiento, EF.Property<Guid>(e, "RowVersion"),
                e.Devolucion!.Id, e.Devolucion.FechaInicio, e.Devolucion.PlazoLimite)).ToListAsync(ct);
    }

    public async Task<IOperacionDevolucionDeposito> AbrirAsync(Guid envioId, CancellationToken ct)
    {
        var db = Abrir();
        try
        {
            await using var lectura = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var actor = await Responsable(db, ct);
            var envio = await db.Envios.AsSplitQuery()
                .Include(e => e.CuentaComercial).ThenInclude(c => c.Operador)
                .Include(e => e.Reglas).ThenInclude(r => r.PoliticaDevolucion)
                .Include(e => e.Bultos).ThenInclude(b => b.Escaneos)
                .Include(e => e.Parada).ThenInclude(p => p!.Bultos)
                .Include(e => e.Devolucion).ThenInclude(d => d!.Bultos).ThenInclude(b => b.Bulto)
                .Include(e => e.Intentos).ThenInclude(i => i.MotivoNoEntrega)
                .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Motivo)
                .SingleOrDefaultAsync(e => e.Id == envioId, ct) ?? throw new AislamientoOperadorException();
            await lectura.CommitAsync(ct);
            return new OperacionDevolucion(db, envio, actor);
        }
        catch { await db.DisposeAsync(); throw; }
    }

    public async Task<IReadOnlyList<RutaPorRendirDto>> ListarAsync(CancellationToken ct)
    {
        await using var db = Abrir();
        await Responsable(db, ct);
        return await db.Set<Ruta>().AsNoTracking().Where(r => r.DespachadaEn != null && r.Rendicion == null)
            .OrderBy(r => r.DespachadaEn).ThenBy(r => r.Id)
            .Select(r => new RutaPorRendirDto(r.Id, EF.Property<Guid>(r, "RowVersion"), r.Repartidor.Email,
                r.Vehiculo.Matricula, r.DespachadaEn!.Value, r.Paradas.Count(p => p.Activa))).ToListAsync(ct);
    }

    public async Task<IOperacionRendicionRuta> AbrirRutaAsync(Guid rutaId, CancellationToken ct)
    {
        var db = Abrir();
        try
        {
            await using var lectura = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            await Responsable(db, ct);
            var ruta = await db.Set<Ruta>().AsSplitQuery()
                .Include(r => r.Repartidor).ThenInclude(r => r.Operador)
                .Include(r => r.Vehiculo).ThenInclude(v => v.Operador)
                .Include(r => r.Rendicion)
                .Include(r => r.Paradas).ThenInclude(p => p.Envio).ThenInclude(e => e.Devolucion)
                .SingleOrDefaultAsync(r => r.Id == rutaId, ct) ?? throw new AislamientoOperadorException();
            await lectura.CommitAsync(ct);
            return new OperacionRendicion(db, ruta);
        }
        catch { await db.DisposeAsync(); throw; }
    }

    private AppDbContext Abrir()
    {
        if (actual.Contexto is not { Origen: OrigenTenant.UsuarioAutenticado, UsuarioId: not null,
                Rol: RolUsuario.Administrador or RolUsuario.Despachador or RolUsuario.OperarioDeposito } tenant)
            throw new AislamientoOperadorException();
        return new(options, new TenantActual(tenant));
    }
    private static async Task<Usuario> Responsable(AppDbContext db, CancellationToken ct) =>
        await db.Set<Usuario>().Include(u => u.Operador).SingleOrDefaultAsync(u => u.Id == db.UsuarioIdActual &&
            u.Activo && u.Rol == db.ContextoTenant!.Rol, ct) ?? throw new AislamientoOperadorException();

    private sealed class OperacionDevolucion(AppDbContext db, Envio envio, Usuario actor) : IOperacionDevolucionDeposito
    {
        public Envio Envio => envio;
        public Usuario Responsable => actor;
        public Guid Version => db.Entry(envio).Property<Guid>("RowVersion").CurrentValue;
        public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
    private sealed class OperacionRendicion(AppDbContext db, Ruta ruta) : IOperacionRendicionRuta
    {
        public Ruta Ruta => ruta;
        public Guid Version => db.Entry(ruta).Property<Guid>("RowVersion").CurrentValue;
        public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
