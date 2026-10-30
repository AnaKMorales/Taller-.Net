using Microsoft.EntityFrameworkCore;

namespace UltimaMilla.Infrastructure.Persistence.Configuracion;

// Las versiones se publican completas y no se editan en sitio, aunque todavía no estén en uso.
internal static class ConfiguracionWriteGuard
{
    internal static void Validate(AppDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var entries = db.ChangeTracker.Entries().ToArray();
        var changed = entries.Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        foreach (var entry in changed)
        {
            if (entry.Entity is ReglasOperativas or CuadroTarifario)
            {
                if (entry.State == EntityState.Added) continue;
                // El alta congela su versión. Nunca se permite descongelar ni alterar sus datos.
                var marca = entry.State == EntityState.Modified &&
                    entry.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name == "EnUso") &&
                    Equals(entry.Property("EnUso").CurrentValue, true) && Equals(entry.Property("EnUso").OriginalValue, false) &&
                    entries.Any(e => e.State == EntityState.Added && e.Entity is Envio &&
                        Equals(e.Property(entry.Entity is ReglasOperativas ? "ReglasKey" : "CuadroTarifarioKey").CurrentValue, entry.Property("Id").CurrentValue));
                if (!marca) throw Inmutable();
            }
            else if (entry.Entity is LineaTarifa or AjusteTarifario or MotivoNoEntrega or PlazoServicio)
            {
                if (entry.State != EntityState.Added) throw Inmutable();
                var fk = entry.Metadata.GetForeignKeys().Single(f => entry.Entity switch
                {
                    LineaTarifa => f.PrincipalEntityType.ClrType == typeof(CuadroTarifario),
                    AjusteTarifario => f.PrincipalEntityType.ClrType == typeof(LineaTarifa),
                    _ => f.PrincipalEntityType.ClrType == typeof(ReglasOperativas)
                });
                var id = entry.Property(fk.Properties.Single(p => p.Name != "TenantId").Name).CurrentValue;
                if (!entries.Any(e => e.State == EntityState.Added && e.Metadata.ClrType == fk.PrincipalEntityType.ClrType &&
                    Equals(e.Property("Id").CurrentValue, id))) throw Inmutable();
            }
            else if (entry.Entity is PoliticaDevolucion)
            {
                if (entry.State != EntityState.Added || !entries.Any(e => e.State == EntityState.Added && e.Entity is ReglasOperativas &&
                    Equals(e.Property("PoliticaDevolucionKey").CurrentValue, entry.Property("Id").CurrentValue))) throw Inmutable();
            }
        }
    }
    private static InvalidOperationException Inmutable() => new("Las versiones de configuración guardadas son inmutables; crea una nueva versión completa.");
}
