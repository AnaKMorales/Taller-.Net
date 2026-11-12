using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Application.Portal;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Tablero;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Backoffice;

public static class BackofficeServiceCollectionExtensions
{
    // INotificadorTablero lo registra la Web (SignalR).
    public static IServiceCollection AgregarBackofficeUltimaMilla(this IServiceCollection services)
    {
        services.AddSingleton<PublicarCambiosInterceptor>();
        services.AddScoped<IConsultasBackoffice, ConsultasBackoffice>();
        services.AddScoped<IDetalleEnvio, DetalleEnvio>();
        services.AddScoped<IConsultasPortal, ConsultasPortal>();
        services.AddScoped<IPanelAvisos, PanelAvisos>();
        services.AddScoped<IReportesGestion, ReportesGestion>();
        services.AddScoped<ITenantResolver, TenantResolver>();
        services.AddScoped<IBuscadorUsuariosLogin, BuscadorUsuariosLogin>();
        return services;
    }
}
