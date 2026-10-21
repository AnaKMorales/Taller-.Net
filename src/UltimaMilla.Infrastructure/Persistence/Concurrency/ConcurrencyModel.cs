using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Concurrency;

internal static class ConcurrencyModel
{
    internal const string RowVersion = "RowVersion";
    internal const string ActiveAssignmentIndex = "IX_Paradas_EnvioId";

    internal static void Configure(ModelBuilder model)
    {
        // Versión lógica administrada por la aplicación; no es rowversion de SQL Server.
        // El DEFAULT permite actualizar filas existentes durante la migración.
        foreach (var type in new[] { typeof(Envio), typeof(Ruta) })
            model.Entity(type).Property<Guid>(RowVersion).IsConcurrencyToken()
                .HasDefaultValueSql("gen_random_uuid()").ValueGeneratedNever();
    }
}
