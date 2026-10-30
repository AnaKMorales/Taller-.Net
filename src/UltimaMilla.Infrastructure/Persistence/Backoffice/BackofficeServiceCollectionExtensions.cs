using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Application.Portal;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Tablero;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Backoffice;

public static class BackofficeServiceCollectionExtensions
{
    // El INotificadorTablero lo registra quien publica (la Web con SignalR).
    // El interceptor se agrega a las opciones del AppDbContext con AddInterceptors.
    public static IServiceCollection AgregarBackofficeUltimaMilla(this IServiceCollection services)
    {
        services.AddSingleton<PublicarCambiosInterceptor>();
        services.AddScoped<IConsultasBackoffice, ConsultasBackoffice>();
        services.AddScoped<IConsultasPortal, ConsultasPortal>();
        services.AddScoped<ITenantResolver, TenantResolver>();
        services.AddScoped<IBuscadorUsuariosLogin, BuscadorUsuariosLogin>();
        return services;
    }
}
