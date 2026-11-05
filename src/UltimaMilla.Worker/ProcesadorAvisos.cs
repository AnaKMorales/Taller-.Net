using System.Text.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Worker;

// Procesa UN mensaje de la cola "avisos". No guarda estado entre mensajes: el TenantContext nace aquí,
// vive solo durante la llamada y nunca se escribe en un campo.
public sealed class ProcesadorAvisos(ITenantResolver resolver, IRegistroIdempotencia idempotencia,
    IManejadorEventoEnvio manejador, ILogger<ProcesadorAvisos> logger) : IManejadorMensajes
{
    public const string Consumidor = "avisos";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoConsumo> ManejarAsync(MensajeRecibido mensaje, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        var evento = Deserializar(mensaje);
        if (evento is null)
        {
            logger.LogWarning("Mensaje {MensajeId} inválido: se descarta.", mensaje.MensajeId);
            return ResultadoConsumo.Descartar;
        }

        try
        {
            // Operador y cuenta salen del mensaje, pero el resolver los verifica contra la base antes de aceptarlos.
            var tenant = await resolver.DesdeMensajeAsync(new TenantMessageScope(evento.OperadorId, evento.CuentaComercialId), cancellationToken);
            var nuevo = await idempotencia.EjecutarUnaVezAsync(tenant, evento.MensajeId, Consumidor,
                ct => manejador.ManejarAsync(evento, tenant, ct), cancellationToken);
            if (!nuevo) logger.LogInformation("Mensaje {MensajeId} duplicado: se confirma sin procesar.", evento.MensajeId);
            return ResultadoConsumo.Confirmar;
        }
        catch (AislamientoOperadorException)
        {
            // Operador o cuenta inexistente o que no coincide: reintentar no lo arregla.
            logger.LogWarning("Mensaje {MensajeId}: operador o cuenta no válidos; se descarta.", evento.MensajeId);
            return ResultadoConsumo.Descartar;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            logger.LogError(error, "Fallo transitorio procesando {MensajeId}; se reintenta.", evento.MensajeId);
            return ResultadoConsumo.Reintentar;
        }
    }

    private static EventoEnvioV1? Deserializar(MensajeRecibido mensaje)
    {
        EventoEnvioV1? evento;
        try { evento = JsonSerializer.Deserialize<EventoEnvioV1>(mensaje.Cuerpo, Json); }
        catch (JsonException) { return null; }
        if (evento is null || evento.Version != 1 || evento.MensajeId == Guid.Empty || evento.OperadorId == Guid.Empty ||
            evento.CuentaComercialId == Guid.Empty || evento.EnvioId == Guid.Empty) return null;
        // El message-id de AMQP es obligatorio y debe coincidir con el del contenido.
        return Guid.TryParse(mensaje.MensajeId, out var id) && id == evento.MensajeId ? evento : null;
    }
}
