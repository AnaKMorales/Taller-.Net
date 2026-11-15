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

    // Acepta los dos contratos que publica el outbox:
    // - V1 (envio.evento.v1): cambios de estado del envío.
    // - V2 (envio.evento.v2): intentos registrados por bulto y entrega parcial; agrega el resultado de cada bulto.
    // La proyección V1 interna mantiene compatibles los manejadores existentes. EntregaWebhooks elige V1/V2
    // por suscripción y recupera el detalle V2 del intento histórico inmutable, también en reenvíos manuales.
    // Cualquier otra versión de entrada se descarta.
    private static EventoEnvioV1? Deserializar(MensajeRecibido mensaje)
    {
        EventoEnvioV1? evento;
        try
        {
            using var documento = JsonDocument.Parse(mensaje.Cuerpo);
            var version = documento.RootElement.ValueKind == JsonValueKind.Object &&
                documento.RootElement.TryGetProperty("version", out var campo) && campo.TryGetInt32(out var numero)
                    ? numero : 0;
            evento = version switch
            {
                EventoEnvioV1.VersionContrato => documento.Deserialize<EventoEnvioV1>(Json),
                EventoEnvioV2.VersionContrato => documento.Deserialize<EventoEnvioV2>(Json) is { Bultos: not null } v2
                    ? ComoV1(v2) : null,
                _ => null
            };
        }
        catch (JsonException) { return null; }
        if (evento is null || evento.Version != 1 || evento.MensajeId == Guid.Empty || evento.OperadorId == Guid.Empty ||
            evento.CuentaComercialId == Guid.Empty || evento.EnvioId == Guid.Empty) return null;
        // El message-id de AMQP es obligatorio y debe coincidir con el del contenido.
        return Guid.TryParse(mensaje.MensajeId, out var id) && id == evento.MensajeId ? evento : null;
    }

    private static EventoEnvioV1 ComoV1(EventoEnvioV2 v2) => new(v2.MensajeId, EventoEnvioV1.VersionContrato,
        v2.OperadorId, v2.CuentaComercialId, v2.EnvioId, v2.EventoId, v2.TipoEvento, v2.Origen, v2.EstadoAnterior,
        v2.EstadoNuevo, v2.OcurridoEn, v2.IdOperacion);
}
