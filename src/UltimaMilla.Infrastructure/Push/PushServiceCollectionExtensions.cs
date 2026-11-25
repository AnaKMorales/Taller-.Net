using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Notificaciones;

namespace UltimaMilla.Infrastructure.Push;

public static class PushServiceCollectionExtensions
{
    // Web: alta/baja de suscripciones (solo necesita la clave pública).
    public static IServiceCollection AgregarSuscripcionesPush(this IServiceCollection services, OpcionesWebPush? opciones)
    {
        opciones ??= new();
        opciones.Validar();
        services.TryAddSingleton(opciones);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<ISuscripcionesPushRepartidor, SuscripcionesPushRepartidor>();
        return services;
    }

    // Worker: envío de los avisos. Sin claves, el manejador no hace nada (lo deja en el log).
    public static IServiceCollection AgregarAvisosPushRepartidor(this IServiceCollection services, OpcionesWebPush? opciones)
    {
        opciones ??= new();
        opciones.Validar();
        services.TryAddSingleton(opciones);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IClienteWebPush, ClienteWebPush>();
        services.TryAddSingleton<AvisoPushRepartidor>();
        return services;
    }
}
