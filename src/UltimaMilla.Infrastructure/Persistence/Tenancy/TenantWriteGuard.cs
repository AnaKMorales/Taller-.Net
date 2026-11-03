using System.Reflection;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

internal static class TenantWriteGuard
{
    internal static async Task ValidateAsync(AppDbContext db, Guid tenant, bool useAsync, CancellationToken cancellationToken = default)
    {
        if (tenant == Guid.Empty) throw new AislamientoOperadorException();
        db.ChangeTracker.DetectChanges();
        var entries = db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        var cache = new Dictionary<(Type, Guid), Guid?>();

        // Se estampan todas las altas antes de validar sus relaciones y de enviarlas a SQL.
        foreach (var entry in entries.Where(e => e.State == EntityState.Added && e.Metadata.FindProperty(TenantModel.TenantId) is not null))
        {
            var property = entry.Property(TenantModel.TenantId);
            var supplied = (Guid)property.CurrentValue!;
            if (supplied != Guid.Empty && supplied != tenant)
                throw new AislamientoOperadorException();
            property.CurrentValue = tenant;
        }
        db.ChangeTracker.DetectChanges();

        foreach (var entry in entries)
        {
            if (entry.Entity is Comercio)
            {
                // Los datos comunes no se editan desde un operador: afectarían a sus otras cuentas.
                if (entry.State != EntityState.Added || !entries.Any(e => e.Entity is CuentaComercial &&
                    e.State == EntityState.Added && Equals(e.Property("ComercioKey").CurrentValue, entry.Property("Id").CurrentValue)))
                    throw new AislamientoOperadorException();
                continue;
            }

            if (entry.Entity is Operador)
            {
                if ((Guid)entry.Property("Id").CurrentValue! != tenant) throw new AislamientoOperadorException();
            }
            else
            {
                var property = entry.Property(TenantModel.TenantId);
                if ((Guid)property.CurrentValue! != tenant ||
                    (entry.State != EntityState.Added && (Guid)property.OriginalValue! != tenant))
                    throw new AislamientoOperadorException();
            }

            if (entry.State != EntityState.Added)
            {
                var stored = useAsync ? await entry.GetDatabaseValuesAsync(cancellationToken) : entry.GetDatabaseValues();
                var actual = stored is null ? (Guid?)null : entry.Entity is Operador
                    ? stored.GetValue<Guid>("Id") : stored.GetValue<Guid>(TenantModel.TenantId);
                if (actual != tenant) throw new AislamientoOperadorException();
            }

            foreach (var fk in entry.Metadata.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType;
                if (principal.ClrType == typeof(Comercio)) continue;
                var idProperty = TenantModel.ReferenceId(fk);
                if (entry.Property(idProperty.Name).CurrentValue is not Guid id)
                {
                    if (fk.IsRequired) throw new AislamientoOperadorException();
                    continue;
                }
                var added = entries.FirstOrDefault(e => e.State == EntityState.Added &&
                    principal.ClrType.IsInstanceOfType(e.Entity) && Equals(e.Property("Id").CurrentValue, id));
                var owner = added is not null ? added.Entity is Operador ? id : (Guid)added.Property(TenantModel.TenantId).CurrentValue!
                    : await StoredTenant(db, principal, id, useAsync, cache, cancellationToken);
                if (owner != tenant) throw new AislamientoOperadorException();
            }
        }
    }

    private static async Task<Guid?> StoredTenant(AppDbContext db, IEntityType type, Guid id, bool useAsync,
        Dictionary<(Type, Guid), Guid?> cache, CancellationToken cancellationToken)
    {
        var key = (type.ClrType, id);
        if (cache.TryGetValue(key, out var value)) return value;
        var method = typeof(TenantWriteGuard).GetMethod(nameof(ReadTenant), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type.ClrType);
        value = await (Task<Guid?>)method.Invoke(null, [db, id, useAsync, cancellationToken])!;
        cache[key] = value;
        return value;
    }

    private static Task<Guid?> ReadTenant<T>(AppDbContext db, Guid id, bool useAsync, CancellationToken cancellationToken) where T : class
    {
        // Lectura interna para validar la pertenencia real, incluso de referencias adjuntadas/desconectadas.
        var query = db.Set<T>().IgnoreQueryFilters().Where(row => EF.Property<Guid>(row, "Id") == id);
        var owner = typeof(T) == typeof(Operador)
            ? query.Select(row => (Guid?)EF.Property<Guid>(row, "Id"))
            : query.Select(row => (Guid?)EF.Property<Guid>(row, TenantModel.TenantId));
        return useAsync ? owner.SingleOrDefaultAsync(cancellationToken) : Task.FromResult(owner.SingleOrDefault());
    }
}
