using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Administracion;

public sealed class IdentidadOperador(DbContextOptions<AppDbContext> options, ITenantActual actual)
    : IAdministracionIdentidadOperador, IConsultaIdentidadOperador
{
    public async Task<EstadoIdentidadOperador> ConsultarAsync(CancellationToken ct = default)
    {
        await using var db = await Abrir(true, ct);
        return Estado(await db.Set<Operador>().SingleAsync(ct));
    }

    public async Task<EstadoIdentidadOperador> ModificarAsync(ModificarIdentidadOperador solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud); ArgumentNullException.ThrowIfNull(solicitud.Datos);
        await using var db = await Abrir(true, ct);
        if (solicitud.VersionEsperada == Guid.Empty) throw new ArgumentException("La versión es obligatoria.");
        var operador = await db.Set<Operador>().SingleAsync(ct);
        if (operador.VersionIdentidad != solicitud.VersionEsperada)
            throw new ConflictoConcurrenciaException(new InvalidOperationException("La identidad visual cambió."));
        var d = solicitud.Datos;
        operador.ConfigurarIdentidad(d.Marca, d.ColorPrimario, d.ColorSecundario, d.TelefonoContacto, d.EmailContacto, d.LogoUrl);
        await db.SaveChangesAsync(ct);
        return Estado(operador);
    }

    public async Task<IdentidadOperadorDto> ActualAsync(CancellationToken ct = default)
    {
        await using var db = await Abrir(false, ct);
        return Datos(await db.Set<Operador>().AsNoTracking().SingleAsync(ct));
    }

    public async Task<IdentidadOperadorDto?> PorSeguimientoAsync(string token, CancellationToken ct = default)
    {
        if (!Guid.TryParseExact(token, "N", out var codigo)) return null;
        await using var db = new AppDbContext(options);
        // Lookup privilegiado mínimo por el mismo token opaco del seguimiento. Sin materializar el grafo.
        return await db.Envios.IgnoreQueryFilters().Where(e => e.CodigoSeguimiento == codigo.ToString("N"))
            .Select(e => new IdentidadOperadorDto(e.CuentaComercial.Operador.Marca,
                e.CuentaComercial.Operador.ColorPrimario, e.CuentaComercial.Operador.ColorSecundario,
                e.CuentaComercial.Operador.TelefonoContacto, e.CuentaComercial.Operador.EmailContacto,
                e.CuentaComercial.Operador.LogoUrl)).SingleOrDefaultAsync(ct);
    }

    private async Task<AppDbContext> Abrir(bool administrador, CancellationToken ct)
    {
        var tenant = actual.Contexto;
        if (tenant is null || administrador && (tenant.Origen != OrigenTenant.UsuarioAutenticado || tenant.Rol != RolUsuario.Administrador))
            throw new AislamientoOperadorException();
        if (tenant.Origen == OrigenTenant.UsuarioAutenticado)
        {
            // Reutiliza las validaciones de identidad persistida y cuenta activa.
            await using var lectura = new AppDbContext(options, new TenantActual(tenant));
            if (!await lectura.Set<Usuario>().AnyAsync(u => u.Id == tenant.UsuarioId && u.Activo && u.Rol == tenant.Rol, ct))
                throw new AislamientoOperadorException();
            if (tenant.Rol == RolUsuario.Comercio && (tenant.CuentaComercialId is not Guid cuenta ||
                !await lectura.Set<CuentaComercial>().AnyAsync(c => c.Id == cuenta && c.Activa && c.Usuarios.Any(u => u.Id == tenant.UsuarioId), ct)))
                throw new AislamientoOperadorException();
        }
        else if (tenant.Origen == OrigenTenant.MensajeWorker)
            await new TenantResolver(options).DesdeMensajeAsync(new(tenant.OperadorId, tenant.CuentaComercialId), ct);
        else throw new AislamientoOperadorException();
        var db = new AppDbContext(options, new TenantActual(tenant));
        if (!await db.Set<Operador>().AnyAsync(ct)) { await db.DisposeAsync(); throw new AislamientoOperadorException(); }
        return db;
    }
    private static EstadoIdentidadOperador Estado(Operador op) => new(Datos(op), op.VersionIdentidad);
    private static IdentidadOperadorDto Datos(Operador op) => new(op.Marca, op.ColorPrimario, op.ColorSecundario,
        op.TelefonoContacto, op.EmailContacto, op.LogoUrl);
}
