using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Configuracion;

namespace UltimaMilla.Infrastructure.Persistence.Configuracion;

public static class ConfiguracionServiceCollectionExtensions
{
    public static IServiceCollection AgregarConfiguracionVersionada(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IConfiguracionVersionada, ConfiguracionVersionada>();
        return services;
    }
}
