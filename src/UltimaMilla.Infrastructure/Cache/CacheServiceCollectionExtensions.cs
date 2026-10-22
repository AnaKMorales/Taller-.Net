using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.Cache;

public static class CacheServiceCollectionExtensions
{
    // Sin conexión a Redis se usa caché en memoria: sirve para desarrollo y pruebas, no para varias instancias.
    public static IServiceCollection AgregarCacheUltimaMilla(this IServiceCollection services,
        string? conexionRedis, CacheOpciones? opciones = null)
    {
        if (string.IsNullOrWhiteSpace(conexionRedis))
        {
            services.AddDistributedMemoryCache();
        }
        else
        {
            services.AddStackExchangeRedisCache(redis =>
            {
                var config = ConfigurationOptions.Parse(conexionRedis);
                // Si Redis no está, se sigue sin caché (CacheSegura) en vez de esperar o fallar al arrancar.
                config.AbortOnConnectFail = false;
                config.ConnectTimeout = 2000;
                config.AsyncTimeout = 2000;
                redis.ConfigurationOptions = config;
                redis.InstanceName = "ultimamilla:";
            });
        }

        services.AddSingleton(opciones ?? new CacheOpciones());
        services.AddSingleton<CacheMetricas>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CacheSegura>();

        services.AddScoped<TarifarioVigente>();
        services.AddScoped(sp => new TarifarioVigenteCacheado(sp.GetRequiredService<TarifarioVigente>(),
            sp.GetRequiredService<CacheSegura>(), sp.GetRequiredService<CacheOpciones>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddScoped<ITarifarioVigente>(sp => sp.GetRequiredService<TarifarioVigenteCacheado>());
        services.AddScoped<IInvalidacionTarifario>(sp => sp.GetRequiredService<TarifarioVigenteCacheado>());

        services.AddScoped<SeguimientoPublico>();
        services.AddScoped<ISeguimientoPublico>(sp => new SeguimientoPublicoCacheado(
            sp.GetRequiredService<SeguimientoPublico>(), sp.GetRequiredService<CacheSegura>(),
            sp.GetRequiredService<CacheOpciones>()));
        return services;
    }
}
