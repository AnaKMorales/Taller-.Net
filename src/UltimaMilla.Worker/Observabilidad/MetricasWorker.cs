using UltimaMilla.Infrastructure.Observabilidad;

namespace UltimaMilla.Worker.Observabilidad;

public sealed class MetricasWorker(SondeoMetricasOperativas sondeo, ILogger<MetricasWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            do
            {
                await MedirAsync("postgresql", sondeo.MedirBaseAsync, stoppingToken);
                await MedirAsync("rabbitmq", sondeo.MedirColasAsync, stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task MedirAsync(string dependencia, Func<CancellationToken, Task> medir, CancellationToken ct)
    {
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TimeSpan.FromSeconds(5));
            await medir(limite.Token);
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            log.LogWarning(error, "No se pudo actualizar la muestra de {Dependencia}.", dependencia);
        }
    }
}
