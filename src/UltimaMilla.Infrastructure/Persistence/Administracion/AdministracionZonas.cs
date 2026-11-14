using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Administracion;

public sealed class AdministracionZonas(DbContextOptions<AppDbContext> options, ITenantActual actual) : IAdministracionZonas
{
    public async Task<IReadOnlyList<ZonaAdministracionDto>> ZonasAsync(CancellationToken ct = default)
    {
        await using var db = Abrir(); await Administrador(db, ct);
        var zonas = await db.Set<ZonaCobertura>().AsNoTracking().Include(z => z.Franjas)
            .OrderBy(z => z.Nombre).ThenBy(z => z.Id).ToListAsync(ct);
        return zonas.Select(z => new ZonaAdministracionDto(z.Id, z.Nombre, z.CodigoPostal, Franjas(z))).ToArray();
    }

    public async Task<ZonaAdministracionDto> CrearZonaAsync(CrearZonaCobertura solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await using var db = Abrir(); var admin = await Administrador(db, ct);
        if (solicitud.Franjas is null || solicitud.Franjas.Count == 0)
            throw new ArgumentException("La zona requiere al menos una franja.");
        var franjas = solicitud.Franjas.Select(NuevaFranja).ToArray();
        var zona = new ZonaCobertura(admin.Operador, Texto(solicitud.Nombre, 200), Texto(solicitud.CodigoPostal, 20), franjas);
        db.Add(zona); await db.SaveChangesAsync(ct);
        return new(zona.Id, zona.Nombre, zona.CodigoPostal, Franjas(zona));
    }

    public async Task<IReadOnlyList<FranjaAdministracionDto>> FranjasAsync(Guid zonaId, CancellationToken ct = default)
    {
        await using var db = Abrir(); await Administrador(db, ct);
        return Franjas(await Zona(db, zonaId, ct));
    }

    public async Task<FranjaAdministracionDto> CrearFranjaAsync(Guid zonaId, CrearFranjaHoraria solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await using var db = Abrir(); await Administrador(db, ct);
        var zona = await Zona(db, zonaId, ct);
        var franja = NuevaFranja(solicitud);
        zona.AgregarFranja(franja);
        db.Add(franja); await db.SaveChangesAsync(ct);
        return Detalle(franja);
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
    private static async Task<ZonaCobertura> Zona(AppDbContext db, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new ArgumentException("La zona es obligatoria.");
        return await db.Set<ZonaCobertura>().Include(z => z.Franjas).SingleOrDefaultAsync(z => z.Id == id, ct)
            ?? throw new AislamientoOperadorException();
    }
    private static FranjaHoraria NuevaFranja(CrearFranjaHoraria solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        return new(Texto(solicitud.Nombre, 200), solicitud.Desde, solicitud.Hasta);
    }
    private static string Texto(string valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor) || valor.Trim().Length > maximo)
            throw new ArgumentException($"El texto es obligatorio y admite hasta {maximo} caracteres.");
        return valor.Trim();
    }
    private static FranjaAdministracionDto Detalle(FranjaHoraria f) => new(f.Id, f.Nombre, f.Desde, f.Hasta);
    private static FranjaAdministracionDto[] Franjas(ZonaCobertura zona) => zona.Franjas
        .OrderBy(f => f.Desde).ThenBy(f => f.Hasta).ThenBy(f => f.Id).Select(Detalle).ToArray();
}
