using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Envios.RegistrarIntento;

namespace UltimaMilla.Infrastructure.Persistence.Intentos;

public static class RegistroIntentosServiceCollectionExtensions
{
    public static IServiceCollection AgregarRegistroIntentos(this IServiceCollection services)
    {
        services.AddScoped<IRegistroIntentoRepository, RegistroIntentoRepository>();
        services.AddScoped<RegistrarIntentoHandler>();
        return services;
    }
}
