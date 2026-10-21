using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

internal static class TenantModel
{
    internal const string TenantId = "TenantId";
    internal const string FilterName = "AislamientoOperador";

    internal static void Configure(ModelBuilder builder, AppDbContext context)
    {
        var roots = builder.Model.GetEntityTypes().Where(t => t.BaseType is null).ToArray();
        var tenantTypes = roots.Where(t => t.ClrType != typeof(Operador) && t.ClrType != typeof(Comercio)).ToArray();
        Expression<Func<Guid>> current = () => context.OperadorIdActual;

        foreach (var type in tenantTypes)
        {
            var entity = builder.Entity(type.Name);
            entity.Property<Guid>(TenantId).IsRequired().ValueGeneratedNever().IsConcurrencyToken();
            entity.HasIndex(TenantId);
            if (type.FindProperty("Id") is not null) entity.HasAlternateKey("Id", TenantId);
            var parameter = Expression.Parameter(type.ClrType, "row");
            var tenant = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(Guid)], parameter, Expression.Constant(TenantId));
            var predicate = Expression.AndAlso(Expression.NotEqual(current.Body, Expression.Constant(Guid.Empty)),
                Expression.Equal(tenant, current.Body));
            entity.HasQueryFilter(FilterName, Expression.Lambda(predicate, parameter));
        }

        // Toda referencia entre datos privados transporta también el operador en su FK.
        foreach (var fk in builder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()).ToArray())
        {
            var principal = fk.PrincipalEntityType.GetRootType();
            if (principal.FindProperty(TenantId) is not { } principalTenant) continue;
            var dependentTenant = fk.DeclaringEntityType.FindProperty(TenantId)!;
            var key = principal.FindKey([principal.FindProperty("Id")!, principalTenant])!;
            fk.SetProperties([.. fk.Properties, dependentTenant], key);
        }

        foreach (var type in tenantTypes)
        {
            var entity = builder.Entity(type.Name);
            entity.HasOne(typeof(Operador)).WithMany().HasForeignKey(TenantId).OnDelete(DeleteBehavior.Restrict);
            if (type.FindProperty("OperadorKey") is not null)
                entity.ToTable(table => table.HasCheckConstraint($"CK_{type.GetTableName()}_OperadorTenant",
                    "\"OperadorId\" = \"TenantId\""));
        }

        builder.Entity<Operador>().HasQueryFilter(FilterName,
            row => context.OperadorIdActual != Guid.Empty && row.Id == context.OperadorIdActual);
        builder.Entity<Comercio>().HasQueryFilter(FilterName,
            row => context.OperadorIdActual != Guid.Empty && row.CuentasComerciales.Any(
                account => EF.Property<Guid>(account, TenantId) == context.OperadorIdActual));
    }
}
