using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Avisos;
using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Infrastructure.Avisos;

public static class AvisosServiceCollectionExtensions
{
    // Requiere las DbContextOptions<AppDbContext> (AgregarPublicacionOutboxPorOperador las registra).
    public static IServiceCollection AgregarEntregaWebhooks(this IServiceCollection services, OpcionesWebhook? opciones = null)
    {
        opciones ??= new();
        opciones.Validar();
        services.AddSingleton(opciones);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IClienteWebhook, ClienteWebhookHttp>();
        services.AddSingleton<IManejadorEventoEnvio, EntregaWebhooks>();
        return services;
    }
}
