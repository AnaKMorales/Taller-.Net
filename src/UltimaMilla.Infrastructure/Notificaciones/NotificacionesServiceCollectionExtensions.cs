using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Notificaciones;
using UltimaMilla.Infrastructure.Avisos;

namespace UltimaMilla.Infrastructure.Notificaciones;

public static class NotificacionesServiceCollectionExtensions
{
    // Llamar después de AgregarEntregaWebhooks y AgregarConsumoMensajes: el manejador del evento pasa a ser el compuesto
    // (aviso al destinatario y después webhooks a los comercios).
    public static IServiceCollection AgregarNotificacionesDestinatario(this IServiceCollection services,
        OpcionesNotificaciones? opciones = null)
    {
        services.AddSingleton(opciones ?? new OpcionesNotificaciones());
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IEnviadorNotificaciones, EnviadorNotificacionesRegistro>();
        services.AddSingleton<NotificacionDestinatario>();
        services.TryAddSingleton<EntregaWebhooks>();
        services.RemoveAll<IManejadorEventoEnvio>();
        services.AddSingleton<IManejadorEventoEnvio>(sp => new ManejadoresEventoEnvio(
            [sp.GetRequiredService<NotificacionDestinatario>(), sp.GetRequiredService<EntregaWebhooks>()]));
        return services;
    }
}
