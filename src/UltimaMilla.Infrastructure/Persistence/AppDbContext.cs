using Microsoft.EntityFrameworkCore;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Usuarios;
using UltimaMilla.Infrastructure.Persistence.Tenancy;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Infrastructure.Persistence.Concurrency;
using Npgsql;

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
    {
        _tenant = tenantActual?.Contexto;
        ChangeTracker.Tracked += (_, args) =>
        {
            // Las FK compuestas se propagan antes de SaveChanges. Las altas necesitan
            // el operador al entrar al tracker para no propagar Guid.Empty a filas existentes.
            // No se reescriben valores explícitos ni entidades leídas o adjuntadas.
            if (args.FromQuery || args.Entry.State != EntityState.Added || OperadorIdActual == Guid.Empty ||
                args.Entry.Metadata.FindProperty(TenantModel.TenantId) is null) return;
            var tenant = args.Entry.Property(TenantModel.TenantId);
            if (Equals(tenant.CurrentValue, Guid.Empty)) tenant.CurrentValue = OperadorIdActual;
        };
    }

    public DbSet<Envio> Envios => Set<Envio>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try { return base.SaveChanges(acceptAllChangesOnSuccess); }
        catch (DbUpdateException error) when (IsConcurrencyConflict(error))
        { throw new ConflictoConcurrenciaException(error); }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try { return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
        catch (DbUpdateException error) when (IsConcurrencyConflict(error))
        { throw new ConflictoConcurrenciaException(error); }
    }

    // Una operación concurrente puede chocar con su índice único antes del UPDATE de la raíz.
    private static bool IsConcurrencyConflict(DbUpdateException error) =>
        error is DbUpdateConcurrencyException || error.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation } postgres && postgres.ConstraintName is
            ConcurrencyModel.ActiveAssignmentIndex or ConcurrencyModel.StopOrderIndex or
            "IX_Devoluciones_EnvioId" or "IX_Devoluciones_EnvioId_TenantId" or
            "IX_Rendiciones_RutaId" or "IX_Rendiciones_RutaId_TenantId" or "IX_IntentosEntrega_EnvioId_Numero" or
            "IX_IntentosEntrega_EnvioId_IdOperacion" or "IX_EventosDeEnvio_EnvioId_IdOperacion" or
            "IX_RecargosVisita_ParadaId" or "IX_RecargosVisita_ParadaId_EnvioId_TenantId";

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

        // Las claves compuestas se referencian desde configuraciones de otros tipos;
        // declarar sus tipos evita depender del orden de aplicación de los mapeos.
        modelBuilder.Entity<IntentoEntrega>().Property<Guid>("EnvioKey");
        modelBuilder.Entity<Parada>().Property<Guid>("EnvioKey");
        modelBuilder.Entity<UltimaMilla.Domain.Deposito.Devolucion>().Property<Guid>("EnvioKey");
        modelBuilder.Entity<Bulto>().Property<Guid>("EnvioKey");
        modelBuilder.Entity<PruebaEntrega>().Property<Guid>("IntentoKey");
        modelBuilder.Entity<DetalleIntentoBulto>().Property<Guid>("IntentoKey");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        TenantModel.Configure(modelBuilder, this);
        CuentaModel.Configure(modelBuilder, this);
        ConcurrencyModel.Configure(modelBuilder);
        foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
