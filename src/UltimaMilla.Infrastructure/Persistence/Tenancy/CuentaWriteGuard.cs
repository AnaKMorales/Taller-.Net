using System.Reflection;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

internal static class CuentaWriteGuard
{
    internal static async Task ValidateAsync(AppDbContext db, TenantContext tenant, bool useAsync, CancellationToken cancellationToken = default)
    {
        if (tenant.CuentaComercialId is not Guid scope) return;
        var accountQuery = db.Set<CuentaComercial>().IgnoreQueryFilters().Where(a => a.Id == scope &&
            EF.Property<Guid>(a, TenantModel.TenantId) == tenant.OperadorId);
        if (!(useAsync ? await accountQuery.AnyAsync(cancellationToken) : accountQuery.Any())) throw new AislamientoOperadorException();
        if (tenant.Origen == OrigenTenant.UsuarioAutenticado)
        {
            var userQuery = db.Set<Usuario>().IgnoreQueryFilters().Where(u => u.Id == tenant.UsuarioId && u.Activo &&
                u.Rol == RolUsuario.Comercio && EF.Property<Guid>(u, "OperadorKey") == tenant.OperadorId &&
                u.CuentasComerciales.Any(a => a.Id == scope));
            if (!(useAsync ? await userQuery.AnyAsync(cancellationToken) : userQuery.Any())) throw new AislamientoOperadorException();
        }
        var entries = db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        var cache = new Dictionary<(Type, Guid), Guid?>();
        foreach (var entry in entries)
        {
            // El primer envío congela las versiones utilizadas; es un efecto del alta, no edición del catálogo.
            if (entry.Entity is ReglasOperativas or CuadroTarifario && entry.State == EntityState.Modified &&
                entry.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name == "EnUso") &&
                Equals(entry.Property("EnUso").CurrentValue, true) && entries.Any(e => e.State == EntityState.Added && e.Entity is Envio &&
                    Equals(e.Property("CuentaComercialKey").CurrentValue, scope) &&
                    Equals(e.Property(entry.Entity is ReglasOperativas ? "ReglasKey" : "CuadroTarifarioKey").CurrentValue, entry.Property("Id").CurrentValue)))
                continue;
            if (entry.Entity is ProgramacionEntrega)
            {
                // Las instantáneas existentes son inmutables, aunque se compartan como valores.
                if (entry.State != EntityState.Added || !await NewSnapshotIsOwned(entry)) throw new AislamientoOperadorException();
                continue;
            }
            if (!CuentaOwnership.Paths.ContainsKey(entry.Metadata.ClrType) || entry.Entity is CuentaComercial)
                throw new AislamientoOperadorException();
            if (entry.State != EntityState.Added && await StoredOwner(entry.Metadata, (Guid)entry.Property("Id").CurrentValue!) != scope)
                throw new AislamientoOperadorException();
            if (await ProposedOwner(entry) != scope) throw new AislamientoOperadorException();
            foreach (var fk in entry.Metadata.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType;
                var isAccountData = CuentaOwnership.Paths.ContainsKey(principal.ClrType);
                if (!isAccountData && principal.ClrType != typeof(ProgramacionEntrega) && principal.ClrType != typeof(Usuario)) continue;
                var idProperty = TenantModel.ReferenceId(fk);
                if (entry.Property(idProperty.Name).CurrentValue is not Guid id) continue;
                if (principal.ClrType == typeof(Usuario))
                {
                    if (tenant.Origen == OrigenTenant.UsuarioAutenticado && id != tenant.UsuarioId) throw new AislamientoOperadorException();
                    continue;
                }
                var pending = FindPending(principal, id);
                if (principal.ClrType == typeof(ProgramacionEntrega))
                {
                    if (pending?.State == EntityState.Added) continue;
                    if (!await SnapshotIsOwned(id)) throw new AislamientoOperadorException();
                }
                else if ((pending?.State == EntityState.Added ? await ProposedOwner(pending) : await StoredOwner(principal, id)) != scope)
                    throw new AislamientoOperadorException();
            }
        }

        EntityEntry? FindPending(IEntityType type, Guid id) => entries.FirstOrDefault(e => type.ClrType.IsInstanceOfType(e.Entity) &&
            e.Metadata.FindProperty("Id") is not null && Equals(e.Property("Id").CurrentValue, id));

        async Task<Guid?> ProposedOwner(EntityEntry entry)
        {
            var path = CuentaOwnership.Paths[entry.Metadata.ClrType];
            if (path.Length == 1) return (Guid?)entry.Property(path[0]).CurrentValue;
            var navigation = entry.Metadata.FindNavigation(path[0])!;
            var fk = navigation.ForeignKey;
            var idProperty = TenantModel.ReferenceId(fk);
            if (navigation.IsOnDependent)
            {
                if (entry.Property(idProperty.Name).CurrentValue is not Guid id) return null;
                var pending = FindPending(navigation.TargetEntityType, id);
                return pending?.State == EntityState.Added ? await ProposedOwner(pending) : await StoredOwner(navigation.TargetEntityType, id);
            }
            var parent = entries.FirstOrDefault(e => fk.DeclaringEntityType.ClrType.IsInstanceOfType(e.Entity) &&
                Equals(e.Property(idProperty.Name).CurrentValue, entry.Property("Id").CurrentValue));
            if (parent is not null) return await ProposedOwner(parent);
            return entry.State == EntityState.Added ? null : await StoredOwner(entry.Metadata, (Guid)entry.Property("Id").CurrentValue!);
        }

        async Task<Guid?> StoredOwner(IEntityType type, Guid id)
        {
            if (cache.TryGetValue((type.ClrType, id), out var owner)) return owner;
            var method = typeof(CuentaWriteGuard).GetMethod(nameof(ReadOwner), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(type.ClrType);
            owner = await (Task<Guid?>)method.Invoke(null, [db, id, useAsync, cancellationToken])!;
            cache[(type.ClrType, id)] = owner;
            return owner;
        }

        async Task<bool> NewSnapshotIsOwned(EntityEntry snapshot)
        {
            var snapshotId = snapshot.Property("Id").CurrentValue;
            var found = false;
            foreach (var parent in entries.Where(e => e.Entity is Envio or EventoDeEnvio))
            foreach (var fk in parent.Metadata.GetForeignKeys().Where(f => f.PrincipalEntityType.ClrType == typeof(ProgramacionEntrega)))
            {
                var property = TenantModel.ReferenceId(fk);
                if (!Equals(parent.Property(property.Name).CurrentValue, snapshotId)) continue;
                found = true;
                if (await ProposedOwner(parent) != scope) return false;
            }
            return found;
        }

        Task<bool> SnapshotIsOwned(Guid id)
        {
            var query = db.Envios.IgnoreQueryFilters().Where(e => EF.Property<Guid>(e, "CuentaComercialKey") == scope &&
                EF.Property<Guid>(e, "ProgramacionKey") == id);
            var events = db.Set<EventoDeEnvio>().IgnoreQueryFilters().Where(e =>
                EF.Property<Guid>(e.Envio, "CuentaComercialKey") == scope &&
                (EF.Property<Guid?>(e, "ProgramacionAnteriorKey") == id || EF.Property<Guid?>(e, "ProgramacionNuevaKey") == id));
            return HasSnapshot();
            async Task<bool> HasSnapshot() => useAsync
                ? await query.AnyAsync(cancellationToken) || await events.AnyAsync(cancellationToken)
                : query.Any() || events.Any();
        }
    }

    private static Task<Guid?> ReadOwner<T>(AppDbContext db, Guid id, bool useAsync, CancellationToken token) where T : class =>
        useAsync ? CuentaOwnership.Query<T>(db, id).SingleOrDefaultAsync(token) : Task.FromResult(CuentaOwnership.Query<T>(db, id).SingleOrDefault());
}
