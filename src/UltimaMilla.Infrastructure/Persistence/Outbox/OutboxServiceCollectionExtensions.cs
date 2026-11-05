using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Infrastructure.Persistence.Outbox;

public static class OutboxServiceCollectionExtensions
{
    // El host registra AppDbContext/tenant dedicado e ITransporteMensajes. No se registra un transporte ficticio.
    public static IServiceCollection AgregarPublicadorOutbox(this IServiceCollection services,
        OpcionesPublicacionOutbox? opciones = null)
    {
        opciones ??= new();
        opciones.Validar();
        services.AddSingleton(opciones);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IPublicadorOutbox, PublicadorOutbox>();
        return services;
    }

    // Para hosts sin usuario (Worker): un contexto ProcesoInterno por operador y por ejecución.
    // Requiere que el host registre ITransporteMensajes.
    public static IServiceCollection AgregarPublicacionOutboxPorOperador(this IServiceCollection services,
        string cadenaConexion, OpcionesPublicacionOutbox? opciones = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cadenaConexion);
        opciones ??= new();
        opciones.Validar();
        services.AddSingleton(opciones);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cadenaConexion).Options);
        services.AddSingleton<PublicacionOutboxPorOperador>();
        services.AddSingleton<IEnumeradorOperadoresOutbox>(sp => sp.GetRequiredService<PublicacionOutboxPorOperador>());
        services.AddSingleton<IPublicadorOutboxPorOperador>(sp => sp.GetRequiredService<PublicacionOutboxPorOperador>());
        return services;
    }

    // Consumo: resolver de tenant por mensaje e idempotencia. Requiere AgregarPublicacionOutboxPorOperador
    // (registra las DbContextOptions) o equivalentes.
    public static IServiceCollection AgregarConsumoMensajes(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<UltimaMilla.Application.Tenancy.ITenantResolver, Tenancy.TenantResolver>();
        services.TryAddSingleton<IRegistroIdempotencia, Inbox.RegistroIdempotencia>();
        return services;
    }
}
