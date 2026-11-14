using System.Net.Mail;
using Npgsql;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Administracion;

public sealed class AdministracionComercios(DbContextOptions<AppDbContext> options, ITenantActual actual) : IAdministracionComercios
{
    public async Task<IReadOnlyList<CuentaAdministracionDto>> CuentasAsync(CancellationToken ct = default)
    {
        await using var db = Abrir(); await Administrador(db, ct);
        return await db.Set<CuentaComercial>().AsNoTracking().OrderBy(c => c.Comercio.RazonSocial).ThenBy(c => c.Id)
            .Select(c => new CuentaAdministracionDto(c.Id, c.Comercio.Id, c.Comercio.RazonSocial, c.Comercio.Telefono, c.Comercio.Email)).ToListAsync(ct);
    }

    public async Task<CuentaAdministracionDto> CrearAsync(CrearComercio solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await using var db = Abrir(); var admin = await Administrador(db, ct);
        var comercio = new Comercio(Texto(solicitud.RazonSocial, 200), Texto(solicitud.Telefono, 50), Email(solicitud.Email));
        var cuenta = new CuentaComercial(admin.Operador, comercio);
        db.Add(cuenta); await db.SaveChangesAsync(ct);
        return new(cuenta.Id, comercio.Id, comercio.RazonSocial, comercio.Telefono, comercio.Email);
    }

    public async Task<IReadOnlyList<UsuarioComercioAdministracionDto>> UsuariosAsync(Guid cuentaId, CancellationToken ct = default)
    {
        await using var db = Abrir(); await Administrador(db, ct);
        await Cuenta(db, cuentaId, ct);
        return await db.Set<Usuario>().AsNoTracking().Where(u => u.CuentasComerciales.Any(c => c.Id == cuentaId))
            .OrderBy(u => u.Email).Select(u => new UsuarioComercioAdministracionDto(u.Id, u.Email, u.Activo)).ToListAsync(ct);
    }

    public async Task<UsuarioComercioAdministracionDto> CrearUsuarioAsync(Guid cuentaId, CrearUsuarioComercio solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await using var db = Abrir(); var admin = await Administrador(db, ct);
        var cuenta = await Cuenta(db, cuentaId, ct);
        var email = Email(solicitud.Email);
        if (solicitud.Contrasena is null || solicitud.Contrasena.Length < ContrasenasUsuario.LongitudMinima || solicitud.Contrasena.Length > 256)
            throw new ArgumentException("La contraseña debe tener entre 8 y 256 caracteres.");
        if (await db.Set<Usuario>().AnyAsync(u => EF.Property<string>(u, "EmailNormalizado") == email, ct))
            throw new InvalidOperationException("El email ya pertenece a un usuario del operador.");
        var usuario = new Usuario(admin.Operador, email, RolUsuario.Comercio);
        usuario.AsignarCuentaComercial(cuenta);
        db.Add(usuario); ContrasenasUsuario.Asignar(db, usuario, solicitud.Contrasena);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Usuarios_OperadorId_EmailNormalizado" })
        { throw new InvalidOperationException("Otro usuario ya utiliza ese email en el operador.", error); }
        return new(usuario.Id, usuario.Email, usuario.Activo);
    }

    private AppDbContext Abrir()
    {
        if (actual.Contexto is not { Origen: OrigenTenant.UsuarioAutenticado, UsuarioId: not null, Rol: RolUsuario.Administrador } tenant)
            throw new AislamientoOperadorException();
        return new(options, new TenantActual(tenant));
    }
    private static async Task<Usuario> Administrador(AppDbContext db, CancellationToken ct) =>
        await db.Set<Usuario>().Include(u => u.Operador).SingleOrDefaultAsync(u => u.Id == db.UsuarioIdActual &&
            u.Activo && u.Rol == RolUsuario.Administrador, ct) ?? throw new AislamientoOperadorException();
    private static async Task<CuentaComercial> Cuenta(AppDbContext db, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new ArgumentException("La cuenta es obligatoria.");
        return await db.Set<CuentaComercial>().SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new AislamientoOperadorException();
    }
    private static string Texto(string valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Trim().Length > maximo)
            throw new ArgumentException($"El texto es obligatorio y admite hasta {maximo} caracteres.");
        return valor.Trim();
    }
    private static string Email(string valor)
    {
        var email = Texto(valor, 254).ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out var direccion) || direccion.Address != email)
            throw new ArgumentException("El email debe ser una dirección válida sin nombre de presentación.");
        return email;
    }
}
