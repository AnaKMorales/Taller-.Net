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
        return await db.Set<CuentaComercial>().AsNoTracking().OrderBy(c => c.RazonSocial).ThenBy(c => c.Id)
            .Select(c => new CuentaAdministracionDto(c.Id, c.Comercio.Id, c.RazonSocial, c.Telefono, c.Email, c.Activa, c.Version)).ToListAsync(ct);
    }

    public async Task<CuentaAdministracionDto> CrearAsync(CrearComercio solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await using var db = Abrir(); var admin = await Administrador(db, ct);
        var comercio = new Comercio(Texto(solicitud.RazonSocial, 200), Texto(solicitud.Telefono, 50), Email(solicitud.Email));
        var cuenta = new CuentaComercial(admin.Operador, comercio);
        db.Add(cuenta); await db.SaveChangesAsync(ct);
        return Detalle(cuenta);
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
        if (!cuenta.Activa) throw new InvalidOperationException("La cuenta comercial está dada de baja.");
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

    public async Task<UsuarioComercioAdministracionDto> ModificarUsuarioAsync(Guid cuentaId, Guid usuarioId,
        ModificarUsuarioComercio solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var email = Email(solicitud.Email);
        return await CambiarUsuario(cuentaId, usuarioId, solicitud.Esperado, email, ct);
    }

    public Task<UsuarioComercioAdministracionDto> DarBajaUsuarioAsync(Guid cuentaId, Guid usuarioId,
        EstadoUsuarioEsperado esperado, CancellationToken ct = default) => CambiarUsuario(cuentaId, usuarioId, esperado, null, ct);

    private async Task<UsuarioComercioAdministracionDto> CambiarUsuario(Guid cuentaId, Guid usuarioId,
        EstadoUsuarioEsperado esperado, string? email, CancellationToken ct)
    {
        if (usuarioId == Guid.Empty || esperado is null || string.IsNullOrWhiteSpace(esperado.Email) || esperado.Activo is null)
            throw new ArgumentException("El usuario y su email/estado leídos son obligatorios.");
        await using var db = Abrir(); await Administrador(db, ct);
        await Cuenta(db, cuentaId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serializa edición/baja del mismo usuario antes de leer su estado actual.
        // La consulta SQL mantiene explícitamente el ámbito del operador.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Usuarios\" WHERE \"Id\" = {usuarioId} AND \"TenantId\" = {db.OperadorIdActual} FOR UPDATE", ct);
        var usuario = await db.Set<Usuario>().Include(u => u.CuentasComerciales)
            .SingleOrDefaultAsync(u => u.Id == usuarioId && u.Rol == RolUsuario.Comercio &&
                u.CuentasComerciales.Any(c => c.Id == cuentaId), ct) ?? throw new AislamientoOperadorException();
        if (usuario.CuentasComerciales.Count != 1)
            throw new InvalidOperationException("El usuario tiene varias cuentas: requiere una operación de administración específica.");
        if (usuario.Email != esperado.Email || usuario.Activo != esperado.Activo.Value)
            throw new ConflictoConcurrenciaException(new InvalidOperationException("El email o estado del usuario cambió."));
        if (email is null) usuario.Desactivar();
        else
        {
            if (await db.Set<Usuario>().AnyAsync(u => u.Id != usuarioId && EF.Property<string>(u, "EmailNormalizado") == email, ct))
                throw new InvalidOperationException("El email ya pertenece a un usuario del operador.");
            usuario.ActualizarEmail(email);
        }
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Usuarios_OperadorId_EmailNormalizado" })
        { throw new InvalidOperationException("Otro usuario ya utiliza ese email en el operador.", error); }
        return new(usuario.Id, usuario.Email, usuario.Activo);
    }

    public async Task<CuentaAdministracionDto> ModificarCuentaAsync(Guid cuentaId, ModificarCuentaComercial solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await using var db = Abrir(); await Administrador(db, ct);
        var cuenta = await Cuenta(db, cuentaId, ct);
        VersionCuenta(cuenta, solicitud.VersionEsperada);
        cuenta.ModificarDatos(Texto(solicitud.RazonSocial, 200), Texto(solicitud.Telefono, 50), Email(solicitud.Email));
        await db.SaveChangesAsync(ct);
        return Detalle(cuenta);
    }

    public async Task<CuentaAdministracionDto> DarBajaCuentaAsync(Guid cuentaId, BajaCuentaComercial solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await using var db = Abrir(); await Administrador(db, ct);
        var cuenta = await Cuenta(db, cuentaId, ct);
        VersionCuenta(cuenta, solicitud.VersionEsperada);
        cuenta.DarBaja(); await db.SaveChangesAsync(ct);
        return Detalle(cuenta);
    }

    private static void VersionCuenta(CuentaComercial cuenta, Guid esperada)
    {
        if (esperada == Guid.Empty) throw new ArgumentException("La versión es obligatoria.");
        if (cuenta.Version != esperada)
            throw new ConflictoConcurrenciaException(new InvalidOperationException("La cuenta comercial cambió."));
    }
    private static CuentaAdministracionDto Detalle(CuentaComercial cuenta) => new(cuenta.Id, cuenta.ComercioId,
        cuenta.RazonSocial, cuenta.Telefono, cuenta.Email, cuenta.Activa, cuenta.Version);

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
        return await db.Set<CuentaComercial>().Include(c => c.Comercio).SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new AislamientoOperadorException();
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
