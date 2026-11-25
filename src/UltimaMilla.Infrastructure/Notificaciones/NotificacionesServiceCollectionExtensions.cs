using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Notificaciones;
using UltimaMilla.Infrastructure.Avisos;
using UltimaMilla.Infrastructure.Push;

namespace UltimaMilla.Infrastructure.Notificaciones;

public static class NotificacionesServiceCollectionExtensions
{
    // Llamar después de AgregarEntregaWebhooks y AgregarConsumoMensajes: el manejador del evento pasa a ser el compuesto
    // (aviso al destinatario y después webhooks a los comercios).
    public static IServiceCollection AgregarNotificacionesDestinatario(this IServiceCollection services,
        OpcionesNotificaciones? opciones = null, OpcionesSmtp? smtp = null)
    {
        services.AddSingleton(opciones ?? new OpcionesNotificaciones());
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        // Con servidor SMTP el aviso llega de verdad; sin él, queda solo en el log.
        if (smtp?.Configurado == true)
        {
            smtp.Validar();
            services.AddSingleton(smtp);
            services.TryAddSingleton<IEnviadorNotificaciones, EnviadorNotificacionesSmtp>();
        }
        services.TryAddSingleton<IEnviadorNotificaciones, EnviadorNotificacionesRegistro>();
        services.AddSingleton<NotificacionDestinatario>();
        services.TryAddSingleton<EntregaWebhooks>();
        services.RemoveAll<IManejadorEventoEnvio>();
        // Orden: destinatario, repartidor (push, si se registró con AgregarAvisosPushRepartidor) y webhooks.
        services.AddSingleton<IManejadorEventoEnvio>(sp => new ManejadoresEventoEnvio(
            new IManejadorEventoEnvio?[] { sp.GetRequiredService<NotificacionDestinatario>(),
                    sp.GetService<AvisoPushRepartidor>(), sp.GetRequiredService<EntregaWebhooks>() }
                .OfType<IManejadorEventoEnvio>().ToArray()));
        return services;
    }
}
