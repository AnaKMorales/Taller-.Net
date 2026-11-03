using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence;

internal static class IntentosBultoWriteGuard
{
    internal static void Validate(AppDbContext db)
    {
        var entries = db.ChangeTracker.Entries().ToArray();
        foreach (var entry in entries.Where(e => e.Entity is IntentoEntrega or DetalleIntentoBulto or PruebaBulto or PruebaEntrega))
        {
            if (entry.State == EntityState.Unchanged) continue;
            if (entry.State != EntityState.Added)
                throw new InvalidOperationException("Los resultados y evidencias persistidos de un intento son históricos e inmutables.");
            var property = entry.Entity is PruebaBulto ? "DetalleKey" : "IntentoKey";
            if (entry.Entity is IntentoEntrega) continue;
            var parentType = entry.Entity is PruebaBulto ? typeof(DetalleIntentoBulto) : typeof(IntentoEntrega);
            var id = entry.Property(property).CurrentValue;
            if (!entries.Any(e => e.State == EntityState.Added && e.Metadata.ClrType == parentType && Equals(e.Property("Id").CurrentValue, id)))
                throw new InvalidOperationException("Los resultados y evidencias nuevos deben guardarse con su intento nuevo.");
        }
    }
}
