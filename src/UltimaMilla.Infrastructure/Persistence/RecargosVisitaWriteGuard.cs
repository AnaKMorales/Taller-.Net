using UltimaMilla.Domain.Despacho;

namespace UltimaMilla.Infrastructure.Persistence;

internal static class RecargosVisitaWriteGuard
{
    internal static void Validate(AppDbContext db)
    {
        var entries = db.ChangeTracker.Entries().ToArray();
        if (entries.Any(e => e.Entity is Parada && e.State == EntityState.Modified && e.Property("RecargoPrevisto").IsModified))
            throw new InvalidOperationException("El recargo previsto se captura al crear la parada y no se edita después.");
        foreach (var entry in entries.Where(e => e.Entity is RecargoVisita && e.State != EntityState.Unchanged))
        {
            if (entry.State != EntityState.Added) throw new InvalidOperationException("Los recargos registrados son históricos.");
            var parada = entries.SingleOrDefault(e => e.Entity is Parada &&
                Equals(e.Property("Id").CurrentValue, entry.Property("ParadaKey").CurrentValue));
            var ruta = parada is null ? null : entries.SingleOrDefault(e => e.Entity is Ruta &&
                Equals(e.Property("Id").CurrentValue, parada.Property("RutaKey").CurrentValue));
            if (parada is null || ruta is null || ruta.Property("DespachadaEn").CurrentValue is null ||
                ruta.State != EntityState.Added && (ruta.State != EntityState.Modified || ruta.Property("DespachadaEn").OriginalValue is not null) ||
                !Equals(entry.Property("Importe").CurrentValue, parada.Property("RecargoPrevisto").CurrentValue) ||
                !Equals(entry.Property("RegistradoEn").CurrentValue, ruta.Property("DespachadaEn").CurrentValue))
                throw new InvalidOperationException("El recargo debe guardarse junto con el primer despacho de su ruta y su importe previsto.");
        }
    }
}
