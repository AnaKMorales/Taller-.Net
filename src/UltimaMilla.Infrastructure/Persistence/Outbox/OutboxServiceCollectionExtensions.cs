using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Infrastructure.Persistence.Outbox;

public static class OutboxServiceCollectionExtensions
{
    // El host registra AppDbContext/tenant dedicado e ITransporteMensajes. No se registra un transporte ficticio.
    public static IServiceCollection AgregarPublicadorOutbox(this IServiceCollection services,
        OpcionesPublicacionOutbox? opciones = null)
    {
        opciones ??= new();
        opciones.Validar();
        services.AddSingleton(opciones);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IPublicadorOutbox, PublicadorOutbox>();
        return services;
    }
}
