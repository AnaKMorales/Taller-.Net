using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Envio> Envios => Set<Envio>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Entidad>();
        foreach (var type in typeof(Entidad).Assembly.GetTypes()
                     .Where(t => !t.IsAbstract && typeof(Entidad).IsAssignableFrom(t)))
        {
            var entity = modelBuilder.Entity(type);
            if (type != typeof(Repartidor)) entity.HasBaseType((Type?)null);
            // Los IDs calculados siguen perteneciendo al dominio. EF mantiene sus propias FK.
            foreach (var property in type.GetProperties().Where(p => p.DeclaringType == type && !p.CanWrite &&
                         !(p.PropertyType.IsGenericType &&
                           p.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>))))
                entity.Ignore(property.Name);
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
