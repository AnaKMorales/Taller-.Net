using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Infrastructure.Mensajeria;

public static class RabbitMqServiceCollectionExtensions
{
    // Lee la sección "RabbitMq". Falla al arrancar si falta usuario o clave; nunca hay valores por defecto secretos.
    public static IServiceCollection AgregarTransporteRabbitMq(this IServiceCollection services, IConfiguration configuracion)
    {
        var opciones = configuracion.GetSection(OpcionesRabbitMq.Seccion).Get<OpcionesRabbitMq>() ?? new OpcionesRabbitMq();
        return services.AgregarTransporteRabbitMq(opciones);
    }

    // Para la Web: solo lee/reencola avisos.dlq. Si no hay usuario y clave configurados (p. ej. desarrollo sin broker)
    // registra una versión "no configurada" y la pantalla lo avisa, en vez de impedir el arranque.
    public static IServiceCollection AgregarColaFallidos(this IServiceCollection services, IConfiguration configuracion)
    {
        var opciones = configuracion.GetSection(OpcionesRabbitMq.Seccion).Get<OpcionesRabbitMq>() ?? new OpcionesRabbitMq();
        if (string.IsNullOrWhiteSpace(opciones.Usuario) || string.IsNullOrWhiteSpace(opciones.Clave))
        {
            services.AddSingleton<IColaFallidos, ColaFallidosNoConfigurada>();
            return services;
        }
        opciones.Validar();
        services.TryAddSingleton(opciones);
        services.TryAddSingleton<ConexionRabbitMq>();
        services.AddSingleton<IColaFallidos, ColaFallidosRabbitMq>();
        return services;
    }

    public static IServiceCollection AgregarTransporteRabbitMq(this IServiceCollection services, OpcionesRabbitMq opciones)
    {
        opciones.Validar();
        services.AddSingleton(opciones);
        services.AddSingleton<ConexionRabbitMq>();
        services.AddSingleton<TopologiaRabbitMq>();
        services.AddSingleton<TransporteRabbitMq>();
        services.AddSingleton<ConsumidorRabbitMq>();
        services.AddSingleton<ITransporteMensajes>(sp => sp.GetRequiredService<TransporteRabbitMq>());
        return services;
    }
}
