using UltimaMilla.Application.Backoffice;

namespace UltimaMilla.Worker;

public sealed class OpcionesCierreOperativo
{
    public TimeSpan Intervalo { get; set; } = TimeSpan.FromMinutes(5);
    public int MaximoDiasPorOperador { get; set; } = 31;
    public void Validar()
    {
        if (Intervalo < TimeSpan.FromSeconds(1) || Intervalo > TimeSpan.FromDays(1) || MaximoDiasPorOperador is < 3 or > 366)
            throw new InvalidOperationException("Intervalo de cierre entre 1 segundo y 1 día; máximo de días entre 3 y 366.");
    }
}

public sealed class CierreOperativoWorker(IProcesamientoOperativo proceso, OpcionesCierreOperativo opciones,
    TimeProvider reloj, ILogger<CierreOperativoWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        opciones.Validar();
        using var timer = new PeriodicTimer(opciones.Intervalo, reloj);
        try
        {
            do { await EjecutarCicloAsync(stoppingToken); } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
    public async Task EjecutarCicloAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> operadores;
        try { operadores = await proceso.ListarOperadoresAsync(ct); }
        catch (Exception error) when (!ct.IsCancellationRequested)
        { log.LogError(error, "No se pudo enumerar operadores para cierre; se reintentará."); return; }
        foreach (var operador in operadores)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var resultado = await proceso.ProcesarAsync(operador, opciones.MaximoDiasPorOperador, ct);
                log.LogInformation("Operador {OperadorId}: {Actualizados} resúmenes recalculados, {Cerrados} cierres nuevos, {Omitidos} bloqueados.",
                    operador, resultado.Actualizados, resultado.Cerrados, resultado.Omitidos);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            { log.LogError(error, "Falló el cierre del operador {OperadorId}; los demás continúan.", operador); }
        }
    }
}
