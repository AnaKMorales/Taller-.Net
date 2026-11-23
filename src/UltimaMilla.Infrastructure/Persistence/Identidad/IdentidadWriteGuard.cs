using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Identidad;

internal static class IdentidadWriteGuard
{
    // Lo único que el login puede guardar sin tenant: intentos fallidos, bloqueo y stamps.
    private static readonly HashSet<string> CamposDelLogin =
    [
        nameof(CuentaAcceso.AccessFailedCount), nameof(CuentaAcceso.LockoutEnd),
        nameof(CuentaAcceso.SecurityStamp), nameof(CuentaAcceso.ConcurrencyStamp)
    ];

    // Excepción acotada a la regla "sin tenant no se escribe": solo modificaciones de CuentaAcceso que no cambian
    // ningún otro campo. Se comparan valores (original contra actual) porque el store de Identity marca toda la
    // entidad como modificada al actualizarla.
    internal static bool EsSoloRegistroDeLogin(AppDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var cambios = Cambios(db);
        return cambios.Length > 0 && cambios.All(e => e.Entity is CuentaAcceso && e.State == EntityState.Modified &&
            e.Properties.All(p => CamposDelLogin.Contains(p.Metadata.Name) || Equals(p.OriginalValue, p.CurrentValue)));
    }

    // Con tenant: toda fila de Identity que se escribe tiene que ser de un Usuario del operador actual.
    internal static async Task ValidateAsync(AppDbContext db, Guid tenant, bool useAsync, CancellationToken token)
    {
        var cambios = Cambios(db).Where(e => IdentidadModel.EsIdentidad(e.Metadata.ClrType)).ToArray();
        if (cambios.Length == 0) return;
        // Una sesión de comercio no administra credenciales.
        if (db.CuentaRestringida) throw new AislamientoOperadorException();
        foreach (var entry in cambios)
        {
            var propietario = entry.Entity is IdentityPasskeyData ? DuenoDeDatosPasskey(db, entry) : entry;
            var campoUsuario = propietario.Property(propietario.Entity is CuentaAcceso ? "Id" : "UserId");
            var usuarioId = (Guid)campoUsuario.CurrentValue!;
            if (propietario.State != EntityState.Added && !Equals(campoUsuario.OriginalValue, usuarioId))
                throw new AislamientoOperadorException();
            if (await Dueno(db, usuarioId, useAsync, token) != tenant) throw new AislamientoOperadorException();
        }
    }

    private static EntityEntry DuenoDeDatosPasskey(AppDbContext db, EntityEntry datos)
    {
        var propiedad = datos.Metadata.FindOwnership()!.Properties.Single().Name;
        var clave = (byte[])datos.Property(propiedad).CurrentValue!;
        return db.ChangeTracker.Entries<IdentityUserPasskey<Guid>>()
            .SingleOrDefault(p => p.Entity.CredentialId.AsSpan().SequenceEqual(clave))
            ?? throw new AislamientoOperadorException();
    }

    private static async Task<Guid?> Dueno(AppDbContext db, Guid usuarioId, bool useAsync, CancellationToken token)
    {
        var nuevo = db.ChangeTracker.Entries<Usuario>().FirstOrDefault(e => e.State == EntityState.Added && e.Entity.Id == usuarioId);
        if (nuevo is not null) return (Guid)nuevo.Property(TenantModel.TenantId).CurrentValue!;
        var query = db.Set<Usuario>().IgnoreQueryFilters().Where(u => u.Id == usuarioId)
            .Select(u => (Guid?)EF.Property<Guid>(u, TenantModel.TenantId));
        return useAsync ? await query.SingleOrDefaultAsync(token) : query.SingleOrDefault();
    }

    private static EntityEntry[] Cambios(AppDbContext db) => db.ChangeTracker.Entries()
        .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
}
