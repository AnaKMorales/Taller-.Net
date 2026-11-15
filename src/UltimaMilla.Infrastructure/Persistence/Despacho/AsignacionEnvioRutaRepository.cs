using System.Data;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Despacho;

public sealed class AsignacionEnvioRutaRepository(DbContextOptions<AppDbContext> options, ITenantActual actual)
    : IAsignacionEnvioRutaRepository
{
    public async Task<IReadOnlyList<EnvioAsignableDto>> ListarEnviosAsync(CancellationToken ct)
    {
        await using var db = Abrir();
        await Responsable(db, ct);
        var envios = await db.Envios.AsNoTracking()
            .Where(e => (e.Estado == EstadoEnvio.EnDeposito || e.Estado == EstadoEnvio.Reprogramado) &&
                e.Parada == null && e.Devolucion == null)
            .Select(e => new { e.Id, e.CodigoSeguimiento, Version = EF.Property<Guid>(e, "RowVersion"), e.Estado,
                Bultos = e.Bultos.Count, Peso = e.Bultos.Sum(b => b.Peso),
                Volumen = e.Bultos.Sum(b => b.Alto * b.Ancho * b.Largo / 1_000_000m),
                e.Programacion.InicioLocal, e.Programacion.FinLocal }).ToListAsync(ct);
        // Los offsets locales se conservan como texto; el orden cronológico se calcula en UTC.
        return envios.OrderBy(e => e.InicioLocal.UtcDateTime).ThenBy(e => e.Id).Select(e =>
            new EnvioAsignableDto(e.Id, e.CodigoSeguimiento, e.Version, e.Estado, e.Bultos, e.Peso, e.Volumen,
                e.InicioLocal.UtcDateTime, e.FinLocal.UtcDateTime)).ToArray();
    }

    public async Task<IReadOnlyList<RutaAsignacionDto>> ListarRutasAsync(CancellationToken ct)
    {
        await using var db = Abrir();
        await using var lectura = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await Responsable(db, ct);
        var rutas = await db.Set<Ruta>().AsSplitQuery()
            .Include(r => r.Vehiculo).Include(r => r.Repartidor)
            .Include(r => r.Paradas).ThenInclude(p => p.Bultos)
            .Where(r => r.DespachadaEn == null && r.Rendicion == null && r.Repartidor.Activo && r.Repartidor.Rol == RolUsuario.Repartidor)
            .OrderBy(r => r.Vehiculo.Matricula).ThenBy(r => r.Id).ToListAsync(ct);
        var resultado = rutas.Select(r => new RutaAsignacionDto(r.Id, Version(db, r), r.Repartidor.Email,
            r.Vehiculo.Matricula, r.MaxParadas, r.Paradas.Count(p => p.Activa),
            (r.Paradas.Max(p => (int?)p.Cod) ?? 0) + 1,
            r.Paradas.Where(p => p.Activa).Sum(p => p.PesoTotalKg), r.Paradas.Where(p => p.Activa).Sum(p => p.VolumenTotalM3),
            r.Vehiculo.CapacidadPesoKg, r.Vehiculo.CapacidadVolumenM3)).ToArray();
        await lectura.CommitAsync(ct);
        return resultado;
    }

    public async Task<RecursosAsignacionDto> RecursosAsync(CancellationToken ct)
    {
        await using var db = Abrir();
        await using var lectura = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await Responsable(db, ct);
        var repartidores = await db.Set<Repartidor>().AsNoTracking().Where(r => r.Activo && r.Rol == RolUsuario.Repartidor)
            .OrderBy(r => r.Email).Select(r => new RepartidorPlanificableDto(r.Id, r.Email)).ToListAsync(ct);
        var vehiculos = await db.Set<Vehiculo>().AsNoTracking().OrderBy(v => v.Matricula)
            .Select(v => new VehiculoPlanificableDto(v.Id, v.Matricula, v.CapacidadPesoKg, v.CapacidadVolumenM3)).ToListAsync(ct);
        await lectura.CommitAsync(ct);
        return new(repartidores, vehiculos);
    }

    public async Task<IOperacionAsignacionEnvioRuta> AbrirAsync(Guid envioId, Guid rutaId, CancellationToken ct)
    {
        var db = Abrir();
        try
        {
            await using var lectura = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var actor = await Responsable(db, ct);
            var envio = await db.Envios.AsSplitQuery()
                .Include(e => e.CuentaComercial).ThenInclude(c => c.Operador)
                .Include(e => e.Programacion).ThenInclude(p => p.Franja)
                .Include(e => e.Reglas)
                .Include(e => e.Bultos).Include(e => e.Parada).Include(e => e.Devolucion).Include(e => e.Intentos)
                .SingleOrDefaultAsync(e => e.Id == envioId, ct) ?? throw new AislamientoOperadorException();
            var ruta = await db.Set<Ruta>().AsSplitQuery()
                .Include(r => r.Repartidor).ThenInclude(r => r.Operador)
                .Include(r => r.Vehiculo).ThenInclude(v => v.Operador)
                .Include(r => r.Rendicion)
                .Include(r => r.Paradas).ThenInclude(p => p.Bultos)
                .SingleOrDefaultAsync(r => r.Id == rutaId, ct) ?? throw new AislamientoOperadorException();
            await lectura.CommitAsync(ct);
            return new Operacion(db, envio, ruta, actor);
        }
        catch { await db.DisposeAsync(); throw; }
    }

    private AppDbContext Abrir()
    {
        if (actual.Contexto is not { Origen: OrigenTenant.UsuarioAutenticado, UsuarioId: not null,
                Rol: RolUsuario.Administrador or RolUsuario.Despachador } tenant)
            throw new AislamientoOperadorException();
        return new(options, new TenantActual(tenant));
    }

    private static async Task<Usuario> Responsable(AppDbContext db, CancellationToken ct) =>
        await db.Set<Usuario>().Include(u => u.Operador).SingleOrDefaultAsync(u => u.Id == db.UsuarioIdActual &&
            u.Activo && u.Rol == db.ContextoTenant!.Rol, ct) ?? throw new AislamientoOperadorException();
    private static Guid Version<T>(AppDbContext db, T entidad) where T : class =>
        db.Entry(entidad).Property<Guid>("RowVersion").CurrentValue;

    private sealed class Operacion(AppDbContext db, Envio envio, Ruta ruta, Usuario actor) : IOperacionAsignacionEnvioRuta
    {
        public Envio Envio => envio;
        public Ruta Ruta => ruta;
        public Usuario Responsable => actor;
        public Guid VersionEnvio => Version(db, envio);
        public Guid VersionRuta => Version(db, ruta);
        public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
