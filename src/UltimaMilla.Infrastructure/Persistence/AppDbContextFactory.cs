using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UltimaMilla.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Permite generar migraciones/SQL sin servidor ni secretos.
        // Para conectar realmente, establecer ConnectionStrings__UltimaMilla.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__UltimaMilla")
            ?? "Host=localhost;Port=5432;Database=ultimamilla;Username=ultimamilla";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        return new AppDbContext(options);
    }
}
