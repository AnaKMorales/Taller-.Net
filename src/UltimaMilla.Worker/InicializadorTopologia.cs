using UltimaMilla.Infrastructure.Mensajeria;

namespace UltimaMilla.Worker;

// Declara exchange y colas antes de que el Worker haga cualquier otra cosa. Si RabbitMQ no
// responde, el proceso falla al arrancar en vez de quedar "sano" sin cola.
public sealed class InicializadorTopologia(TopologiaRabbitMq topologia, ILogger<InicializadorTopologia> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await topologia.DeclararAsync(cancellationToken);
        logger.LogInformation("Topología de RabbitMQ declarada.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
