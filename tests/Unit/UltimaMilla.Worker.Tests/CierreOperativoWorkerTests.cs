using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Worker;

namespace UltimaMilla.Worker.Tests;

public sealed class CierreOperativoWorkerTests
{
    private sealed class Proceso : IProcesamientoOperativo
    {
        public IReadOnlyList<Guid> Operadores { get; set; } = [Guid.NewGuid(), Guid.NewGuid()];
        public List<Guid> Llamadas { get; } = [];
        public bool FallarListado { get; set; }
        public Guid? FallarOperador { get; set; }
        public TaskCompletionSource Ejecutado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<Guid>> ListarOperadoresAsync(CancellationToken ct) => FallarListado
            ? throw new InvalidOperationException("No disponible") : Task.FromResult(Operadores);
        public Task<ResultadoRecalculo> ProcesarAsync(Guid id, int maximo, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Llamadas.Add(id); Ejecutado.TrySetResult();
            if (id == FallarOperador) throw new InvalidOperationException("Fallo de operador");
            return Task.FromResult(new ResultadoRecalculo(2, 1, 0));
        }
    }

    [Fact]
    public async Task FalloDeUnOperadorNoImpideElSiguienteYFalloDeListadoSeReintenta()
    {
        var proceso = new Proceso(); proceso.FallarOperador = proceso.Operadores[0];
        using var worker = new CierreOperativoWorker(proceso, new(), TimeProvider.System, NullLogger<CierreOperativoWorker>.Instance);
        await worker.EjecutarCicloAsync(default);
        Assert.Equal(proceso.Operadores, proceso.Llamadas);
        proceso.FallarListado = true;
        await worker.EjecutarCicloAsync(default);
        Assert.Equal(2, proceso.Llamadas.Count);
        proceso.FallarListado = false;
        await worker.EjecutarCicloAsync(default);
        Assert.Equal(4, proceso.Llamadas.Count);
    }

    [Fact]
    public async Task ServicioProcesaAlArrancarYSeDetieneSinEsperarElProximoIntervalo()
    {
        var proceso = new Proceso();
        using var worker = new CierreOperativoWorker(proceso, new() { Intervalo = TimeSpan.FromDays(1) },
            TimeProvider.System, NullLogger<CierreOperativoWorker>.Instance);
        await worker.StartAsync(default);
        await proceso.Ejecutado.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await worker.StopAsync(limite.Token);
        Assert.NotEmpty(proceso.Llamadas);
        Assert.Throws<InvalidOperationException>(() => new OpcionesCierreOperativo { MaximoDiasPorOperador = 2 }.Validar());
    }
}
