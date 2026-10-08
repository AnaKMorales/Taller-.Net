using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Envio> Envios { get; set; }
}
