using Microsoft.Extensions.Logging;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Configuracion;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Configuracion;

public sealed class ConfiguracionVersionada(DbContextOptions<AppDbContext> opciones, ITenantActual actual,
    TimeProvider reloj, IInvalidacionTarifario invalidacion, ILogger<ConfiguracionVersionada> logger) : IConfiguracionVersionada
{
    private AppDbContext Contexto()
    {
        var tenant = actual.Contexto;
        if (tenant is not { Origen: OrigenTenant.UsuarioAutenticado, Rol: RolUsuario.Administrador, UsuarioId: not null })
            throw new AislamientoOperadorException();
        return new(opciones, new TenantActual(tenant));
    }

    private static async Task Autorizar(AppDbContext db, CancellationToken ct)
    {
        if (!await db.Set<Usuario>().AnyAsync(u => u.Id == db.UsuarioIdActual && u.Activo && u.Rol == RolUsuario.Administrador, ct))
            throw new AislamientoOperadorException();
    }

    public async Task<IReadOnlyList<VersionConfiguracionDto>> ListarReglasAsync(CancellationToken ct = default)
    {
        await using var db = Contexto(); await Autorizar(db, ct);
        return await db.Set<ReglasOperativas>().AsNoTracking().OrderByDescending(r => r.Version)
            .Select(r => new VersionConfiguracionDto(r.Id, r.Version, r.VigenteDesde, r.EnUso)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ZonaConfiguracionDto>> ListarZonasAsync(CancellationToken ct = default)
    {
        await using var db = Contexto(); await Autorizar(db, ct);
        return await db.Set<ZonaCobertura>().AsNoTracking().OrderBy(z => z.Nombre)
            .Select(z => new ZonaConfiguracionDto(z.Id, z.Nombre, z.CodigoPostal)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VersionConfiguracionDto>> ListarTarifariosAsync(CancellationToken ct = default)
    {
        await using var db = Contexto(); await Autorizar(db, ct);
        return await db.Set<CuadroTarifario>().AsNoTracking().OrderByDescending(r => r.Version)
            .Select(r => new VersionConfiguracionDto(r.Id, r.Version, r.VigenteDesde, r.EnUso)).ToListAsync(ct);
    }

    private static IQueryable<ReglasOperativas> Reglas(AppDbContext db) => db.Set<ReglasOperativas>().AsNoTracking().AsSplitQuery()
        .Include(r => r.PoliticaDevolucion).Include(r => r.PlazosServicio).Include(r => r.MotivosNoEntrega);
    private static IQueryable<CuadroTarifario> Tarifarios(AppDbContext db) => db.Set<CuadroTarifario>().AsNoTracking().AsSplitQuery()
        .Include(r => r.Lineas).ThenInclude(l => l.Ajustes)
        .Include(r => r.Lineas).ThenInclude(l => l.Zona);
    private static DetalleReglasDto Detalle(ReglasOperativas r) => new(new(r.Id, r.Version, r.VigenteDesde, r.EnUso),
        new(r.MaxIntentosEntrega, r.PlazoEntreIntentosHoras, r.TiposPrueba.Order().ToArray(),
            new(r.PoliticaDevolucion.DiasEnDeposito, r.PoliticaDevolucion.PlazoMaximoDevolucionDias, r.PoliticaDevolucion.DevolverAlAgotarIntentos),
            r.PlazosServicio.OrderBy(p => p.ModalidadServicio).Select(p => new PlazoServicioDto(p.ModalidadServicio, p.PlazoComprometidoHoras, p.HoraCorte)).ToArray(),
            r.MotivosNoEntrega.OrderBy(m => m.Nombre).Select(m => new MotivoNoEntregaDto(m.Nombre, m.Descripcion, m.Responsable, m.RequiereEvidencia, m.EsReintentable, m.Activo)).ToArray(),
            r.PermiteEntregaParcial, r.AccionPendientes, r.ImporteRecargoNuevaVisita));
    private static DetalleTarifarioDto Detalle(CuadroTarifario r) => new(new(r.Id, r.Version, r.VigenteDesde, r.EnUso),
        r.Lineas.Select(l => new LineaTarifaDto(l.ZonaId, l.ModalidadServicio, l.PesoDesde, l.PesoHasta, l.VolumenHasta, l.Precio,
            l.Ajustes.Select(a => new AjusteTarifaDto(a.Tipo, a.ValorPorcentaje)).ToArray())).ToArray());

    public async Task<DetalleReglasDto?> ObtenerReglasAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = Contexto(); await Autorizar(db, ct);
        var r = await Reglas(db).SingleOrDefaultAsync(r => r.Id == id, ct);
        return r is null ? null : Detalle(r);
    }
    public async Task<DetalleTarifarioDto?> ObtenerTarifarioAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = Contexto(); await Autorizar(db, ct);
        var r = await Tarifarios(db).SingleOrDefaultAsync(r => r.Id == id, ct);
        return r is null ? null : Detalle(r);
    }
    public async Task<DetalleReglasDto?> ReglasVigentesAsync(CancellationToken ct = default)
    {
        await using var db = Contexto(); await Autorizar(db, ct);
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var r = await Reglas(db).Where(r => r.VigenteDesde <= ahora).OrderByDescending(r => r.VigenteDesde)
            .ThenByDescending(r => r.Version).FirstOrDefaultAsync(ct);
        return r is null ? null : Detalle(r);
    }
    public async Task<DetalleTarifarioDto?> TarifarioVigenteAsync(CancellationToken ct = default)
    {
        await using var db = Contexto(); await Autorizar(db, ct);
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var r = await Tarifarios(db).Where(r => r.VigenteDesde <= ahora).OrderByDescending(r => r.VigenteDesde)
            .ThenByDescending(r => r.Version).FirstOrDefaultAsync(ct);
        return r is null ? null : Detalle(r);
    }

    private DateTime Fecha(DateTime? fecha)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        if (fecha.HasValue && (fecha.Value.Kind != DateTimeKind.Utc || fecha.Value < ahora))
            throw new ArgumentException("La vigencia explícita debe ser UTC y no estar en el pasado.");
        return fecha ?? ahora;
    }
    private static void VerificarVersion(int esperada, int ultima)
    {
        if (esperada < 0) throw new ArgumentOutOfRangeException(nameof(esperada));
        if (esperada != ultima) throw new ConflictoConcurrenciaException(new InvalidOperationException(
            "Se creó otra versión. Consulta nuevamente la configuración antes de guardar."));
    }
    private static Task<Operador> BloquearOperador(AppDbContext db, CancellationToken ct) => db.Set<Operador>()
        .FromSqlInterpolated($"SELECT * FROM \"Operadores\" WHERE \"Id\" = {db.OperadorIdActual} FOR UPDATE")
        .SingleAsync(ct);

    public async Task<VersionCreadaDto> CrearReglasAsync(NuevaVersionReglas solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var datos = solicitud.Datos ?? throw new ArgumentException("Faltan los datos de las reglas.");
        if (datos.TiposPrueba is null || datos.TiposPrueba.Count == 0 || datos.Plazos is null || datos.Plazos.Count == 0 ||
            datos.Motivos is null || datos.Motivos.Count == 0 || datos.Devolucion is null)
            throw new ArgumentException("Se requieren pruebas, plazos, motivos y política de devolución.");
        await using var db = Contexto(); await Autorizar(db, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var operador = await BloquearOperador(db, ct);
        var ultima = await db.Set<ReglasOperativas>().MaxAsync(r => (int?)r.Version, ct) ?? 0;
        VerificarVersion(solicitud.UltimaVersionEsperada, ultima);
        var fecha = Fecha(solicitud.VigenteDesde);
        var politica = datos.Devolucion;
        var reglas = new ReglasOperativas(operador, checked(ultima + 1), fecha, datos.MaxIntentosEntrega,
            datos.PlazoEntreIntentosHoras, datos.TiposPrueba, new(politica.DiasEnDeposito, politica.PlazoMaximoDevolucionDias, politica.DevolverAlAgotarIntentos),
            datos.PermiteEntregaParcial, datos.AccionPendientes, datos.ImporteRecargoNuevaVisita);
        foreach (var p in datos.Plazos)
        {
            ArgumentNullException.ThrowIfNull(p);
            _ = new PlazoServicio(reglas, p.Modalidad, p.Horas, p.HoraCorte);
        }
        if (datos.Motivos.Any(m => m is null) || datos.Motivos.Select(m => m.Nombre?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != datos.Motivos.Count)
            throw new ArgumentException("Los motivos no pueden ser nulos ni repetir su nombre.");
        foreach (var m in datos.Motivos)
            _ = new MotivoNoEntrega(reglas, m.Nombre, m.Descripcion, m.Responsable, m.RequiereEvidencia, m.EsReintentable, m.Activo);
        if (!reglas.MotivosNoEntrega.Any(m => m.Activo)) throw new ArgumentException("Debe existir un motivo activo.");
        db.Add(reglas); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(new(reglas.Id, reglas.Version, fecha, false));
    }

    public async Task<VersionCreadaDto> CrearTarifarioAsync(NuevaVersionTarifario solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (solicitud.Lineas is null || solicitud.Lineas.Count == 0 || solicitud.Lineas.Any(l => l is null))
            throw new ArgumentException("Se requiere al menos una línea válida.");
        await using var db = Contexto(); await Autorizar(db, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var operador = await BloquearOperador(db, ct);
        var ultima = await db.Set<CuadroTarifario>().MaxAsync(r => (int?)r.Version, ct) ?? 0;
        VerificarVersion(solicitud.UltimaVersionEsperada, ultima);
        var fecha = Fecha(solicitud.VigenteDesde);
        var cuadro = new CuadroTarifario(operador, checked(ultima + 1), fecha);
        var zonasId = solicitud.Lineas.Select(l => l.ZonaId).Distinct().ToArray();
        var zonas = await db.Set<ZonaCobertura>().Where(z => zonasId.Contains(z.Id)).ToDictionaryAsync(z => z.Id, ct);
        if (zonas.Count != zonasId.Length) throw new AislamientoOperadorException();
        foreach (var l in solicitud.Lineas)
        {
            var linea = new LineaTarifa(cuadro, zonas[l.ZonaId], l.Modalidad, l.PesoDesde, l.PesoHasta, l.VolumenHasta, l.Precio);
            if (l.Ajustes is null || l.Ajustes.Any(a => a is null)) throw new ArgumentException("Ajustes inválidos.");
            foreach (var a in l.Ajustes) _ = new AjusteTarifario(linea, a.Tipo, a.ValorPorcentaje);
            if (l.Ajustes.Where(a => a.Tipo == TipoAjuste.Bonificacion).Sum(a => a.ValorPorcentaje) >
                100 + l.Ajustes.Where(a => a.Tipo == TipoAjuste.Recargo).Sum(a => a.ValorPorcentaje))
                throw new ArgumentException("Los ajustes producirían un precio negativo.");
        }
        db.Add(cuadro); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var revisar = false;
        try { await invalidacion.InvalidarAsync(operador.Id, ct); }
        catch (Exception error)
        {
            revisar = true;
            logger.LogWarning(error, "La versión {Version} se guardó, pero falló la solicitud de invalidación del tarifario de {Operador}.", cuadro.Version, operador.Id);
        }
        return new(new(cuadro.Id, cuadro.Version, fecha, false), revisar);
    }
}
