using System.Data;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Despacho;

public sealed class DespachoPendientes(DbContextOptions<AppDbContext> options, ITenantActual actual, TimeProvider reloj)
    : IDespachoPendientes
{
    public async Task<PlanificacionPendientesDto> ConsultarAsync(Guid envioId, CancellationToken ct)
    {
        await using var db = Abrir();
        await using var lectura = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        await Responsable(db, ct);
        var envio = await CargarEnvio(db, envioId, ct);
        var rutas = await db.Set<Ruta>().Where(r => r.DespachadaEn == null && r.Rendicion == null &&
                r.Repartidor.Activo && r.Repartidor.Rol == RolUsuario.Repartidor &&
                r.Paradas.Count(p => p.Activa) < r.MaxParadas)
            .OrderBy(r => r.Vehiculo.Matricula).ThenBy(r => r.Id)
            .Select(r => new RutaPlanificableDto(r.Id, EF.Property<Guid>(r, "RowVersion"), r.Repartidor.Email,
                r.Vehiculo.Matricula, (r.Paradas.Max(p => (int?)p.Cod) ?? 0) + 1)).ToListAsync(ct);
        var repartidores = await db.Set<Repartidor>().Where(r => r.Activo && r.Rol == RolUsuario.Repartidor)
            .OrderBy(r => r.Email).Select(r => new RepartidorPlanificableDto(r.Id, r.Email)).ToListAsync(ct);
        var vehiculos = await db.Set<Vehiculo>().OrderBy(v => v.Matricula)
            .Select(v => new VehiculoPlanificableDto(v.Id, v.Matricula, v.CapacidadPesoKg, v.CapacidadVolumenM3)).ToListAsync(ct);
        var franjas = envio.Zona.Franjas.OrderBy(f => f.Desde)
            .Select(f => new FranjaPendientesDto(f.Id, f.Nombre, f.Desde, f.Hasta)).ToArray();
        await lectura.CommitAsync(ct);
        return new(envio.Id, Version(db, envio), envio.Reglas.AccionPendientes, envio.Reglas.ImporteRecargoNuevaVisita,
            franjas, rutas, repartidores, vehiculos);
    }

    public async Task<RutaPlanificableDto> CrearRutaAsync(CrearRutaPendientes solicitud, CancellationToken ct)
    {
        await using var db = Abrir();
        await Responsable(db, ct);
        var repartidor = await db.Set<Repartidor>().Include(r => r.Operador)
            .SingleOrDefaultAsync(r => r.Id == solicitud.RepartidorId && r.Activo && r.Rol == RolUsuario.Repartidor, ct)
            ?? throw new AislamientoOperadorException();
        var vehiculo = await db.Set<Vehiculo>().Include(v => v.Operador)
            .SingleOrDefaultAsync(v => v.Id == solicitud.VehiculoId, ct) ?? throw new AislamientoOperadorException();
        var ruta = new Ruta(repartidor, vehiculo, solicitud.MaxParadas);
        db.Add(ruta);
        await db.SaveChangesAsync(ct);
        return new(ruta.Id, Version(db, ruta), repartidor.Email, vehiculo.Matricula, 1);
    }

    public async Task<VisitaPendientesDto> AsignarAsync(Guid rutaId, AsignarPendientes solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud.Programacion);
        await using var db = Abrir();
        await using var lectura = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var actor = await Responsable(db, ct);
        var envio = await CargarEnvio(db, solicitud.EnvioId, ct);
        var ruta = await db.Set<Ruta>().AsSplitQuery().Include(r => r.Vehiculo).ThenInclude(v => v.Operador)
            .Include(r => r.Repartidor).ThenInclude(r => r.Operador)
            .Include(r => r.Rendicion)
            .Include(r => r.Paradas).ThenInclude(p => p.Bultos).ThenInclude(b => b.Bulto)
            .SingleOrDefaultAsync(r => r.Id == rutaId, ct) ?? throw new AislamientoOperadorException();
        VerificarVersion(db, envio, solicitud.VersionEsperadaEnvio);
        VerificarVersion(db, ruta, solicitud.VersionEsperadaRuta);
        if (!ruta.Repartidor.Activo || ruta.Repartidor.Rol != RolUsuario.Repartidor)
            throw new InvalidOperationException("El repartidor de la ruta no está activo.");
        var datos = solicitud.Programacion;
        var franja = envio.Zona.Franjas.SingleOrDefault(f => f.Id == datos.FranjaId)
            ?? throw new ArgumentException("La franja no pertenece a la zona del envío.");
        var programacion = new ProgramacionEntrega(franja, datos.Fecha, datos.InicioLocal, datos.FinLocal);
        await lectura.CommitAsync(ct);

        var ahora = reloj.GetUtcNow().UtcDateTime;
        if (envio.Bultos.Any(b => b.Estado == EstadoBulto.NoEntregado))
            envio.Reprogramar(programacion, actor, ahora);
        else if (envio.Programacion != programacion)
            throw new InvalidOperationException("La visita preparada ya tiene otra programación.");

        var parada = ruta.AgregarParada(envio, solicitud.Orden, solicitud.LlegadaEstimada.UtcDateTime, actor, ahora);
        await db.SaveChangesAsync(ct);
        return new(parada.Id, Version(db, envio), Version(db, ruta),
            parada.Bultos.Select(b => b.Bulto.Id).ToArray(), parada.PesoTotalKg, parada.VolumenTotalM3, parada.RecargoPrevisto);
    }

    public async Task<DevolucionPendientesDto> DevolverAsync(Guid envioId, Guid versionEsperadaEnvio, CancellationToken ct)
    {
        await using var db = Abrir();
        await using var lectura = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var actor = await Responsable(db, ct);
        var envio = await CargarEnvio(db, envioId, ct);
        VerificarVersion(db, envio, versionEsperadaEnvio);
        await lectura.CommitAsync(ct);

        var devolucion = envio.IniciarDevolucion(actor, reloj.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return new(devolucion.Id, Version(db, envio), devolucion.Bultos.Select(b => b.Bulto.Id).ToArray());
    }

    private AppDbContext Abrir()
    {
        if (actual.Contexto is not { Origen: OrigenTenant.UsuarioAutenticado, UsuarioId: not null,
                Rol: RolUsuario.Administrador or RolUsuario.Despachador } tenant)
            throw new AislamientoOperadorException();
        return new(options, new TenantActual(tenant));
    }

    private static async Task<Usuario> Responsable(AppDbContext db, CancellationToken ct) =>
        await db.Set<Usuario>().Include(u => u.Operador).SingleOrDefaultAsync(
            u => u.Id == db.UsuarioIdActual && u.Activo && u.Rol == db.ContextoTenant!.Rol, ct)
        ?? throw new AislamientoOperadorException();

    private static async Task<Envio> CargarEnvio(AppDbContext db, Guid envioId, CancellationToken ct)
    {
        var envio = await db.Envios.AsSplitQuery()
            .Include(e => e.CuentaComercial).ThenInclude(c => c.Operador)
            .Include(e => e.Zona).ThenInclude(z => z.Franjas)
            .Include(e => e.Programacion).ThenInclude(p => p.Franja)
            .Include(e => e.Reglas).ThenInclude(r => r.PoliticaDevolucion)
            .Include(e => e.Bultos)
            .Include(e => e.Parada)
            .Include(e => e.Devolucion)
            .Include(e => e.Intentos).ThenInclude(i => i.MotivoNoEntrega)
            .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Motivo)
            .SingleOrDefaultAsync(e => e.Id == envioId, ct) ?? throw new AislamientoOperadorException();
        if (envio.Estado != EstadoEnvio.EntregadoParcialmente || envio.Parada is not null || envio.Devolucion is not null ||
            !envio.Bultos.Any(b => b.Estado is EstadoBulto.NoEntregado or EstadoBulto.Reprogramado))
            throw new InvalidOperationException("El envío debe tener una entrega parcial con bultos pendientes y sin otra visita o devolución.");
        return envio;
    }

    private static Guid Version<T>(AppDbContext db, T entidad) where T : class =>
        db.Entry(entidad).Property<Guid>("RowVersion").CurrentValue;

    private static void VerificarVersion<T>(AppDbContext db, T entidad, Guid esperada) where T : class
    {
        if (esperada == Guid.Empty) throw new ArgumentException("La versión esperada es obligatoria.");
        if (Version(db, entidad) != esperada)
            throw new ConflictoConcurrenciaException(new InvalidOperationException("La versión esperada no coincide con la actual."));
    }
}
