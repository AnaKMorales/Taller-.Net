using System.Security.Claims;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

public sealed class TenantResolver(DbContextOptions<AppDbContext> options) : ITenantResolver
{
    // ASP.NET debe validar previamente la cookie/JWT. Este servicio no valida firmas de tokens.
    public async Task<TenantContext> DesdeClaimsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var identities = principal.Identities.Where(i => i.IsAuthenticated).ToArray();
        if (identities.Length != 1) throw new AislamientoOperadorException();
        var claims = identities[0].Claims.ToArray();
        var op = RequiredId(claims, "operador_id");
        var user = RequiredId(claims, ClaimTypes.NameIdentifier, "sub");
        var roleText = Unique(claims, ClaimTypes.Role, "role");
        if (roleText is null || !Enum.GetNames<RolUsuario>().Contains(roleText, StringComparer.OrdinalIgnoreCase) ||
            !Enum.TryParse<RolUsuario>(roleText, true, out var role)) throw new AislamientoOperadorException();
        await using var db = new AppDbContext(options, TenantActual.ParaOperador(op));
        var stored = await db.Set<Usuario>().Where(u => u.Id == user).Select(u => new { u.Activo, u.Rol }).SingleOrDefaultAsync(cancellationToken);
        if (stored is null || !stored.Activo || stored.Rol != role) throw new AislamientoOperadorException();
        var account = OptionalId(claims, "cuenta_comercial_id");
        var commerce = OptionalId(claims, "comercio_id");
        if (role != RolUsuario.Comercio)
        {
            if (account is not null || commerce is not null) throw new AislamientoOperadorException();
            return TenantContext.ParaUsuario(op, user, role);
        }
        if (account is null && commerce is null) throw new AislamientoOperadorException();
        var matches = await db.Set<CuentaComercial>().Where(c => c.Activa && c.Usuarios.Any(u => u.Id == user) &&
                (account == null || c.Id == account) && (commerce == null || EF.Property<Guid>(c, "ComercioKey") == commerce))
            .Select(c => c.Id).Take(2).ToListAsync(cancellationToken);
        if (matches.Count != 1) throw new AislamientoOperadorException();
        return TenantContext.ParaUsuario(op, user, role, matches[0]);
    }

    public async Task<TenantContext> DesdeMensajeAsync(TenantMessageScope mensaje, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        if (mensaje.OperadorId == Guid.Empty || mensaje.CuentaComercialId == Guid.Empty) throw new AislamientoOperadorException();
        await using var db = new AppDbContext(options, TenantActual.ParaOperador(mensaje.OperadorId));
        if (!await db.Set<Operador>().AnyAsync(cancellationToken) || (mensaje.CuentaComercialId is Guid account &&
            !await db.Set<CuentaComercial>().AnyAsync(c => c.Id == account, cancellationToken))) throw new AislamientoOperadorException();
        // Se devuelve un valor nuevo por mensaje; nunca se modifica un contexto compartido.
        return TenantContext.ParaMensaje(mensaje.OperadorId, mensaje.CuentaComercialId);
    }

    private static string? Unique(Claim[] claims, params string[] names)
    {
        var values = claims.Where(c => names.Contains(c.Type)).Select(c => c.Value).Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length > 1) throw new AislamientoOperadorException();
        return values.SingleOrDefault();
    }

    private static Guid RequiredId(Claim[] claims, params string[] names) => OptionalId(claims, names) ?? throw new AislamientoOperadorException();
    private static Guid? OptionalId(Claim[] claims, params string[] names)
    {
        var values = claims.Where(c => names.Contains(c.Type)).Select(c => c.Value).ToArray();
        if (values.Length == 0) return null;
        var ids = new HashSet<Guid>();
        foreach (var value in values)
        {
            if (!Guid.TryParse(value, out var id) || id == Guid.Empty) throw new AislamientoOperadorException();
            ids.Add(id);
        }
        if (ids.Count != 1) throw new AislamientoOperadorException();
        return ids.Single();
    }
}
