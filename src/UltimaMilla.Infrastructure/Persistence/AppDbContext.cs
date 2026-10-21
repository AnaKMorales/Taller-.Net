using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Usuarios;
using UltimaMilla.Infrastructure.Persistence.Tenancy;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    // Se captura una vez: cambiar el servicio de identidad no cambia un contexto ya utilizado.
    private readonly TenantContext? _tenant;
    internal TenantContext? ContextoTenant => _tenant;
    internal Guid OperadorIdActual => _tenant?.OperadorId ?? Guid.Empty;
    internal Guid? CuentaComercialIdActual => _tenant?.CuentaComercialId;
    internal Guid UsuarioIdActual => _tenant?.UsuarioId ?? Guid.Empty;
    internal bool CuentaRestringida => CuentaComercialIdActual.HasValue;
    internal bool EsSeguimientoPublico => _tenant?.Origen == OrigenTenant.SeguimientoPublico;
    internal Guid? EnvioPublicoId => _tenant?.EnvioPublicoId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantActual? tenantActual = null) : base(options)
        => _tenant = tenantActual?.Contexto;

    public DbSet<Envio> Envios => Set<Envio>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.AddInterceptors(TenantSaveChangesInterceptor.Instance);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<Entidad>();
        modelBuilder.Ignore<GeoPunto>();
        modelBuilder.Ignore<ArchivoRef>();
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
        TenantModel.Configure(modelBuilder, this);
        CuentaModel.Configure(modelBuilder, this);
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
