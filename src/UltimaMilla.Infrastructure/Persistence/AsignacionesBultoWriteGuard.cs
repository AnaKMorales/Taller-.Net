using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Deposito;

namespace UltimaMilla.Infrastructure.Persistence;

internal static class AsignacionesBultoWriteGuard
{
    internal static void Validate(AppDbContext db)
    {
        var entries = db.ChangeTracker.Entries().ToArray();
        foreach (var entry in entries.Where(e => e.Entity is BultoParada or BultoDevolucion && e.State != EntityState.Unchanged))
        {
            var padre = entry.Entity is BultoParada ? typeof(Parada) : typeof(Devolucion);
            var fk = entry.Entity is BultoParada ? "ParadaKey" : "DevolucionKey";
            if (entry.State != EntityState.Added || !entries.Any(p => p.Metadata.ClrType == padre && p.State == EntityState.Added &&
                Equals(p.Property("Id").CurrentValue, entry.Property(fk).CurrentValue)))
                throw new InvalidOperationException("La composición de una parada o devolución se guarda al crearla y es histórica.");
        }
    }
}
