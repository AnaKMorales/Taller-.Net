using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Worker;

// En cada ciclo: lista los operadores con mensajes pendientes y publica un lote para cada uno.
// - Sin estado entre operadores ni entre ciclos: cada publicación crea su contexto y lo descarta.
// - Un error de un operador (o al listar) se registra y no frena a los demás ni al próximo ciclo.
// - Apagado: el token cancela la publicación en curso; la transacción se revierte y la fila queda pendiente.
public sealed class PublicacionOutboxWorker(IEnumeradorOperadoresOutbox enumerador, IPublicadorOutboxPorOperador publicador,
    OpcionesPublicacionPeriodica opciones, TimeProvider reloj, ILogger<PublicacionOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        opciones.Validar();
        using var temporizador = new PeriodicTimer(opciones.Intervalo, reloj);
        try
        {
            do { await EjecutarCicloAsync(stoppingToken); }
            while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        logger.LogInformation("Publicación del outbox detenida.");
    }

    public async Task EjecutarCicloAsync(CancellationToken token)
    {
        IReadOnlyList<Guid> operadores;
        try { operadores = await enumerador.ListarConPendientesAsync(token); }
        catch (Exception error) when (!token.IsCancellationRequested)
        {
            logger.LogError(error, "No se pudo listar los operadores con mensajes pendientes; se reintenta en el próximo ciclo.");
            return;
        }

        await Parallel.ForEachAsync(operadores, new ParallelOptions
        {
            MaxDegreeOfParallelism = opciones.OperadoresEnParalelo,
            CancellationToken = token,
        }, async (operadorId, ct) =>
        {
            try
            {
                // Si el lote salió lleno puede haber más: se sigue hasta vaciar o hasta que llegue el apagado.
                int publicados;
                do
                {
                    publicados = await publicador.PublicarLoteAsync(operadorId, opciones.MaximoMensajesPorLote, ct);
                    if (publicados > 0)
                        logger.LogInformation("Operador {OperadorId}: {Cantidad} mensajes publicados.", operadorId, publicados);
                } while (publicados >= opciones.MaximoMensajesPorLote);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                // Los mensajes que fallaron quedan pendientes y se reintentan en el próximo ciclo.
                logger.LogError(error, "Falló la publicación del operador {OperadorId}; los demás continúan.", operadorId);
            }
        });
    }
}
