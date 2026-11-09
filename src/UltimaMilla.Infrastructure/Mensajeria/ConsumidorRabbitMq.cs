using System.Text;
using System.Diagnostics;
using UltimaMilla.Infrastructure.Observabilidad;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Infrastructure.Mensajeria;

// Consume la cola "avisos" con ack manual. Solo traduce AMQP <-> IManejadorMensajes:
//   Confirmar  -> ack
//   Descartar  -> nack sin reencolar (la cola lo manda a avisos.dlq)
//   Reintentar -> copia a avisos.espera.5s / 30s / 2m (según los intentos previos) y ack del original;
//                 agotadas las esperas, nack sin reencolar (dlq).
// Entrega al menos una vez: un mensaje sin ack vuelve a entregarse, por eso el manejador es idempotente.
public sealed class ConsumidorRabbitMq(ConexionRabbitMq conexion, TopologiaRabbitMq topologia, OpcionesRabbitMq opciones,
    IManejadorMensajes manejador, ILogger<ConsumidorRabbitMq> logger) : IAsyncDisposable
{
    public const string EncabezadoIntentos = "x-intentos";

    private readonly CancellationTokenSource _forzarCierre = new();
    private IChannel? _canal;
    private IChannel? _canalReintentos;
    private string? _etiqueta;
    private int _enCurso;

    public async Task IniciarAsync(CancellationToken cancellationToken = default)
    {
        await topologia.DeclararAsync(cancellationToken);
        var amqp = await conexion.ObtenerAsync(cancellationToken);
        _canalReintentos = await amqp.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), cancellationToken);
        _canal = await amqp.CreateChannelAsync(cancellationToken: cancellationToken);
        await _canal.BasicQosAsync(0, opciones.Prefetch, global: false, cancellationToken);

        var consumidor = new AsyncEventingBasicConsumer(_canal);
        consumidor.ReceivedAsync += AlRecibirAsync;
        _etiqueta = await _canal.BasicConsumeAsync(opciones.ColaAvisos, autoAck: false, consumidor, cancellationToken);
        logger.LogInformation("Consumiendo la cola {Cola}.", opciones.ColaAvisos);
    }

    // Deja de recibir, espera a los mensajes en curso y, si no terminan a tiempo, los cancela.
    // Lo que quede sin ack lo reentrega el broker al cerrar el canal.
    public async Task DetenerAsync(CancellationToken cancellationToken = default)
    {
        if (_canal is { IsOpen: true } && _etiqueta is not null)
            await _canal.BasicCancelAsync(_etiqueta, cancellationToken: cancellationToken);
        var limite = DateTime.UtcNow + opciones.EsperaCierre;
        while (Volatile.Read(ref _enCurso) > 0 && DateTime.UtcNow < limite && !cancellationToken.IsCancellationRequested)
            await Task.Delay(25, CancellationToken.None);
        if (Volatile.Read(ref _enCurso) > 0)
        {
            logger.LogWarning("Se cancelan {Cantidad} mensajes en curso por apagado; el broker los reentregará.", _enCurso);
            await _forzarCierre.CancelAsync();
            for (var i = 0; i < 100 && Volatile.Read(ref _enCurso) > 0; i++) await Task.Delay(25, CancellationToken.None);
        }
        await CerrarCanalesAsync();
        logger.LogInformation("Consumidor detenido.");
    }

    private async Task AlRecibirAsync(object remitente, BasicDeliverEventArgs entrega)
    {
        Interlocked.Increment(ref _enCurso);
        var actividadAnterior = Activity.Current;
        var inicio = Stopwatch.GetTimestamp();
        var resultadoMetrica = "Error";
        Activity? actividad = null;
        IDisposable? contextoLog = null;
        // El contexto padre procede exclusivamente del mensaje, nunca del hilo consumidor.
        Activity.Current = null;
        try
        {
            var canal = _canal!;
            var propiedades = entrega.BasicProperties;
            actividad = DiagnosticoMensajeria.Fuente.StartActivity("avisos process", ActivityKind.Consumer,
                DiagnosticoMensajeria.LeerContexto(propiedades.Headers));
            actividad?.SetTag("messaging.system", "rabbitmq");
            actividad?.SetTag("messaging.destination.name", opciones.ColaAvisos);
            actividad?.SetTag("messaging.operation.name", "process");
            actividad?.SetTag("messaging.message.id", propiedades.MessageId);
            contextoLog = logger.BeginScope(new Dictionary<string, object?>
            {
                ["TraceId"] = actividad?.TraceId.ToString(),
                ["MensajeId"] = propiedades.MessageId
            });
            var intentos = PoliticaReintentos.LeerIntentos(propiedades.Headers);
            var mensaje = new MensajeRecibido(propiedades.MessageId, propiedades.Type,
                Encoding.UTF8.GetString(entrega.Body.Span), intentos);

            ResultadoConsumo resultado;
            try { resultado = await manejador.ManejarAsync(mensaje, _forzarCierre.Token); }
            catch (OperationCanceledException) when (_forzarCierre.IsCancellationRequested)
            {
                await canal.BasicNackAsync(entrega.DeliveryTag, false, requeue: true);
                return;
            }
            catch (Exception error)
            {
                // El manejador debería clasificar sus errores; si se escapa uno, se trata como transitorio.
                logger.LogError(error, "El manejador lanzó una excepción para el mensaje {MensajeId}.", propiedades.MessageId);
                resultado = ResultadoConsumo.Reintentar;
            }

            var decision = PoliticaReintentos.Decidir(resultado, intentos, opciones);
            resultadoMetrica = resultado.ToString();
            actividad?.SetTag("ultimamilla.resultado", resultado.ToString());
            if (resultado != ResultadoConsumo.Confirmar) actividad?.SetStatus(ActivityStatusCode.Error);
            if (resultado == ResultadoConsumo.Reintentar && decision.Accion == AccionFinal.EnviarAMuertos)
                logger.LogWarning("Mensaje {MensajeId} agotó {Intentos} reintentos; va a {Dlq}.",
                    propiedades.MessageId, intentos, opciones.ColaMuertos);

            switch (decision.Accion)
            {
                case AccionFinal.Confirmar:
                    await canal.BasicAckAsync(entrega.DeliveryTag, false);
                    break;
                case AccionFinal.EnviarAMuertos:
                    // nack sin reencolar: la cola avisos lo dead-letterea a avisos.dlq.
                    await canal.BasicNackAsync(entrega.DeliveryTag, false, requeue: false);
                    break;
                default:
                    try
                    {
                        await RepublicarEnEsperaAsync(entrega, decision);
                        await canal.BasicAckAsync(entrega.DeliveryTag, false);
                    }
                    catch (Exception error)
                    {
                        // No se pudo mover a la espera: se devuelve al broker en vez de perderlo.
                        logger.LogError(error, "No se pudo enviar {MensajeId} a la cola de espera.", propiedades.MessageId);
                        await canal.BasicNackAsync(entrega.DeliveryTag, false, requeue: true);
                    }
                    break;
            }
        }
        catch (Exception error)
        {
            actividad?.SetStatus(ActivityStatusCode.Error);
            // Un fallo del canal no debe tumbar el proceso: el broker reentrega lo que quedó sin ack.
            logger.LogError(error, "Error al confirmar o rechazar la entrega {Entrega}.", entrega.DeliveryTag);
        }
        finally
        {
            MetricasOperativas.Procesamiento(resultadoMetrica, Stopwatch.GetElapsedTime(inicio).TotalSeconds);
            contextoLog?.Dispose();
            actividad?.Dispose();
            Activity.Current = actividadAnterior;
            Interlocked.Decrement(ref _enCurso);
        }
    }

    private async Task RepublicarEnEsperaAsync(BasicDeliverEventArgs entrega, DecisionReintento decision)
    {
        var origen = entrega.BasicProperties;
        var encabezados = new Dictionary<string, object?>(origen.Headers ?? new Dictionary<string, object?>())
        {
            [EncabezadoIntentos] = decision.IntentosNuevos,
        };
        var propiedades = new BasicProperties
        {
            MessageId = origen.MessageId,
            Type = origen.Type,
            ContentType = origen.ContentType,
            ContentEncoding = origen.ContentEncoding,
            Timestamp = origen.Timestamp,
            DeliveryMode = DeliveryModes.Persistent,
            Headers = encabezados,
        };
        var espera = decision.ColaEspera!;
        // Exchange por defecto: la routing key es el nombre de la cola. Con confirmaciones, falla si no se confirma.
        await _canalReintentos!.BasicPublishAsync("", espera, mandatory: true, propiedades, entrega.Body);
    }

    private async Task CerrarCanalesAsync()
    {
        if (_canal is not null) { await _canal.DisposeAsync(); _canal = null; }
        if (_canalReintentos is not null) { await _canalReintentos.DisposeAsync(); _canalReintentos = null; }
    }

    public async ValueTask DisposeAsync()
    {
        await CerrarCanalesAsync();
        _forzarCierre.Dispose();
    }
}
