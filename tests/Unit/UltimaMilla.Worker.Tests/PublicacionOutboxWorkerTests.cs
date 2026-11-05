using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Worker;

namespace UltimaMilla.Worker.Tests;

public sealed class PublicacionOutboxWorkerTests
{
    private sealed class Enumerador(Func<IReadOnlyList<Guid>> listar) : IEnumeradorOperadoresOutbox
    {
        public Task<IReadOnlyList<Guid>> ListarConPendientesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(listar());
    }

    private sealed class Publicador(Func<Guid, int, CancellationToken, Task<int>> publicar) : IPublicadorOutboxPorOperador
    {
        public Task<int> PublicarLoteAsync(Guid operadorId, int maximoMensajes = 100, CancellationToken cancellationToken = default) =>
            publicar(operadorId, maximoMensajes, cancellationToken);
    }

    private static PublicacionOutboxWorker Crear(IEnumeradorOperadoresOutbox e, IPublicadorOutboxPorOperador p,
        int lote = 100, int paralelo = 2) =>
        new(e, p, new OpcionesPublicacionPeriodica { Intervalo = TimeSpan.FromMilliseconds(20), MaximoMensajesPorLote = lote,
            OperadoresEnParalelo = paralelo }, TimeProvider.System, NullLogger<PublicacionOutboxWorker>.Instance);

    [Fact]
    public async Task UnErrorDeUnOperadorNoFrenaALosDemas()
    {
        Guid malo = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        var publicados = new ConcurrentBag<Guid>();
        var worker = Crear(new Enumerador(() => [malo, b, c]), new Publicador((id, _, _) =>
        {
            if (id == malo) throw new IOException("Sin confirmación");
            publicados.Add(id);
            return Task.FromResult(1);
        }), lote: 5, paralelo: 1);

        await worker.EjecutarCicloAsync(CancellationToken.None); // no lanza

        Assert.Equal(new[] { b, c }.Order(), publicados.Order());
    }

    [Fact]
    public async Task UnErrorAlListarNoLanzaYElSiguienteCicloFunciona()
    {
        var llamadas = 0;
        var op = Guid.NewGuid();
        var publicados = 0;
        var worker = Crear(new Enumerador(() => ++llamadas == 1 ? throw new InvalidOperationException("BD caída") : [op]),
            new Publicador((_, _, _) => Task.FromResult(++publicados * 0)));

        await worker.EjecutarCicloAsync(CancellationToken.None);
        Assert.Equal(0, publicados);
        await worker.EjecutarCicloAsync(CancellationToken.None);
        Assert.Equal(1, publicados);
    }

    [Fact]
    public async Task SigueConElMismoOperadorMientrasElLoteSalgaLleno()
    {
        var restantes = 7;
        var lotes = new List<int>();
        var worker = Crear(new Enumerador(() => [Guid.NewGuid()]), new Publicador((_, max, _) =>
        {
            var n = Math.Min(max, restantes);
            restantes -= n;
            lotes.Add(n);
            return Task.FromResult(n);
        }), lote: 3);

        await worker.EjecutarCicloAsync(CancellationToken.None);

        Assert.Equal([3, 3, 1], lotes);
    }

    [Fact]
    public async Task ElApagadoCancelaLaPublicacionEnCursoYTerminaSinError()
    {
        var empezo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelado = false;
        var worker = Crear(new Enumerador(() => [Guid.NewGuid()]), new Publicador(async (_, _, ct) =>
        {
            empezo.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) { cancelado = true; throw; }
            return 0;
        }));

        await worker.StartAsync(CancellationToken.None);
        await empezo.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(cancelado);
        Assert.True(worker.ExecuteTask!.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task RepiteCiclosPeriodicamente()
    {
        var ciclos = 0;
        var tres = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = Crear(new Enumerador(() => { if (Interlocked.Increment(ref ciclos) == 3) tres.TrySetResult(); return []; }),
            new Publicador((_, _, _) => Task.FromResult(0)));
        await worker.StartAsync(CancellationToken.None);
        await tres.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData(0, 100, 1)]
    [InlineData(2000, 0, 1)]
    [InlineData(2000, 100, 0)]
    public void OpcionesInvalidasSeRechazan(int intervaloMs, int lote, int paralelo) =>
        Assert.Throws<InvalidOperationException>(() => new OpcionesPublicacionPeriodica
        { Intervalo = TimeSpan.FromMilliseconds(intervaloMs), MaximoMensajesPorLote = lote, OperadoresEnParalelo = paralelo }.Validar());
}
