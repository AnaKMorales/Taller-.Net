using System.Net.Mail;
using Npgsql;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Administracion;

public sealed class AdministracionFlota(DbContextOptions<AppDbContext> options, ITenantActual actual) : IAdministracionFlota
{
    public async Task<IReadOnlyList<RepartidorAdministracionDto>> RepartidoresAsync(CancellationToken ct = default)
    {
        await using var db = Abrir();
        await Administrador(db, ct);
        return await db.Set<Repartidor>().AsNoTracking().OrderBy(r => r.Email)
            .Select(r => new RepartidorAdministracionDto(r.Id, r.Email, r.Activo)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VehiculoAdministracionDto>> VehiculosAsync(CancellationToken ct = default)
    {
        await using var db = Abrir();
        await Administrador(db, ct);
        return await db.Set<Vehiculo>().AsNoTracking().OrderBy(v => v.Matricula)
            .Select(v => new VehiculoAdministracionDto(v.Id, v.Matricula, v.CapacidadPesoKg, v.CapacidadVolumenM3)).ToListAsync(ct);
    }

    public async Task<RepartidorAdministracionDto> CrearRepartidorAsync(CrearRepartidor solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await using var db = Abrir();
        var admin = await Administrador(db, ct);
        var email = solicitud.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !MailAddress.TryCreate(email, out var direccion) || direccion.Address != email)
            throw new ArgumentException("El email debe ser una dirección válida de hasta 254 caracteres.");
        if (solicitud.Contrasena is null || solicitud.Contrasena.Length < ContrasenasUsuario.LongitudMinima || solicitud.Contrasena.Length > 256)
            throw new ArgumentException("La contraseña debe tener entre 8 y 256 caracteres.");
        if (await db.Set<Usuario>().AnyAsync(u => EF.Property<string>(u, "EmailNormalizado") == email, ct))
            throw new InvalidOperationException("El email ya pertenece a un usuario del operador.");
        var repartidor = new Repartidor(admin.Operador, email);
        db.Add(repartidor);
        ContrasenasUsuario.Asignar(db, repartidor, solicitud.Contrasena);
        await Guardar(db, ct);
        return new(repartidor.Id, repartidor.Email, repartidor.Activo);
    }

    public async Task<VehiculoAdministracionDto> CrearVehiculoAsync(CrearVehiculo solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await using var db = Abrir();
        var admin = await Administrador(db, ct);
        var matricula = solicitud.Matricula?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(matricula) || matricula.Length > 50)
            throw new ArgumentException("La matrícula debe tener entre 1 y 50 caracteres.");
        var normalizada = matricula.ToLowerInvariant();
        if (await db.Set<Vehiculo>().AnyAsync(v => EF.Property<string>(v, "MatriculaNormalizada") == normalizada, ct))
            throw new InvalidOperationException("La matrícula ya existe en el operador.");
        var vehiculo = new Vehiculo(admin.Operador, matricula, solicitud.CapacidadPesoKg, solicitud.CapacidadVolumenM3);
        db.Add(vehiculo);
        await Guardar(db, ct);
        return new(vehiculo.Id, vehiculo.Matricula, vehiculo.CapacidadPesoKg, vehiculo.CapacidadVolumenM3);
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
    private static async Task Guardar(AppDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName:
                "IX_Usuarios_OperadorId_EmailNormalizado" or "IX_Vehiculos_OperadorId_MatriculaNormalizada" })
        { throw new InvalidOperationException("Otro registro ya utiliza ese email o matrícula en el operador.", error); }
    }
}
