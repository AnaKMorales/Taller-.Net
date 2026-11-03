using Microsoft.EntityFrameworkCore.ChangeTracking;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Concurrency;

internal static class AggregateConcurrency
{
    // Las reglas de capacidad y de estado usan colecciones. Cambiar solo un hijo también
    // debe competir por la versión de la raíz leída al tomar esa decisión.
    internal static bool Prepare(AppDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var tracked = db.ChangeTracker.Entries().ToArray();
        var changed = tracked.Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        var roots = new HashSet<EntityEntry>();
        foreach (var entry in changed)
        {
            switch (entry.Entity)
            {
                case Envio or Ruta:
                    roots.Add(entry);
                    break;
                case Parada:
                    Parents(entry, typeof(Envio), "EnvioKey");
                    Parents(entry, typeof(Ruta), "RutaKey");
                    break;
                case BultoParada:
                    foreach (var parada in Parents(entry, typeof(Parada), "ParadaKey"))
                    {
                        Parents(parada, typeof(Envio), "EnvioKey");
                        Parents(parada, typeof(Ruta), "RutaKey");
                    }
                    foreach (var bulto in Parents(entry, typeof(Bulto), "BultoKey"))
                        Parents(bulto, typeof(Envio), "EnvioKey");
                    break;
                case BultoDevolucion:
                    foreach (var devolucion in Parents(entry, typeof(Devolucion), "DevolucionKey"))
                        Parents(devolucion, typeof(Envio), "EnvioKey");
                    foreach (var bulto in Parents(entry, typeof(Bulto), "BultoKey"))
                        Parents(bulto, typeof(Envio), "EnvioKey");
                    break;
                case RecargoVisita:
                    Parents(entry, typeof(Envio), "EnvioKey");
                    foreach (var parada in Parents(entry, typeof(Parada), "ParadaKey"))
                        Parents(parada, typeof(Ruta), "RutaKey");
                    break;
                case Bulto or EventoDeEnvio or IntentoEntrega or Devolucion or Discrepancia:
                    Parents(entry, typeof(Envio), "EnvioKey");
                    break;
                case EscaneoBulto:
                    foreach (var bulto in Parents(entry, typeof(Bulto), "BultoKey"))
                        Parents(bulto, typeof(Envio), "EnvioKey");
                    Parents(entry, typeof(Ruta), "RutaKey");
                    break;
                case PruebaEntrega:
                    foreach (var intento in Parents(entry, typeof(IntentoEntrega), "IntentoKey"))
                        Parents(intento, typeof(Envio), "EnvioKey");
                    break;
                case DetalleIntentoBulto:
                    foreach (var intento in Parents(entry, typeof(IntentoEntrega), "IntentoKey"))
                        Parents(intento, typeof(Envio), "EnvioKey");
                    foreach (var bulto in Parents(entry, typeof(Bulto), "BultoKey"))
                        Parents(bulto, typeof(Envio), "EnvioKey");
                    break;
                case PruebaBulto:
                    foreach (var detalle in Parents(entry, typeof(DetalleIntentoBulto), "DetalleKey"))
                        foreach (var intento in Parents(detalle, typeof(IntentoEntrega), "IntentoKey"))
                            Parents(intento, typeof(Envio), "EnvioKey");
                    foreach (var prueba in Parents(entry, typeof(PruebaEntrega), "PruebaKey"))
                        foreach (var intento in Parents(prueba, typeof(IntentoEntrega), "IntentoKey"))
                            Parents(intento, typeof(Envio), "EnvioKey");
                    break;
                case Rendicion:
                    Parents(entry, typeof(Ruta), "RutaKey");
                    break;
            }
        }

        var promoted = false;
        foreach (var root in roots)
        {
            var version = root.Property(ConcurrencyModel.RowVersion);
            if (root.State != EntityState.Added && Equals(version.OriginalValue, Guid.Empty))
                throw MissingAggregate();
            if (root.State == EntityState.Deleted) continue;
            promoted |= root.State == EntityState.Unchanged;
            version.CurrentValue = Guid.NewGuid();
            if (root.State != EntityState.Added) version.IsModified = true;
        }
        return promoted;

        IEnumerable<EntityEntry> Parents(EntityEntry child, Type type, string foreignKey)
        {
            var property = child.Property(foreignKey);
            var ids = new HashSet<Guid>();
            if (property.CurrentValue is Guid current) ids.Add(current);
            if (child.State != EntityState.Added && property.OriginalValue is Guid original) ids.Add(original);
            var parents = new List<EntityEntry>();
            foreach (var id in ids)
            {
                var parent = tracked.SingleOrDefault(e => e.Metadata.ClrType == type && Equals(e.Property("Id").CurrentValue, id));
                if (parent is null) throw MissingAggregate();
                parents.Add(parent);
                if (type == typeof(Envio) || type == typeof(Ruta)) roots.Add(parent);
            }
            return parents;
        }
    }

    private static InvalidOperationException MissingAggregate() => new(
        "Para modificar datos operativos, carga el envío o la ruta y las relaciones necesarias con seguimiento en el mismo DbContext. " +
        "No se puede recuperar su versión recién al guardar ni adjuntar un grafo sin la versión original.");
}
