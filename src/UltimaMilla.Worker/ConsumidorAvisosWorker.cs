using UltimaMilla.Infrastructure.Mensajeria;

namespace UltimaMilla.Worker;

// Arranca el consumidor y, al apagar, deja de recibir, espera a los mensajes en curso y cierra los canales.
public sealed class ConsumidorAvisosWorker(ConsumidorRabbitMq consumidor, ILogger<ConsumidorAvisosWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await consumidor.IniciarAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        logger.LogInformation("Apagando el consumidor de avisos.");
        await consumidor.DetenerAsync();
    }
}
