using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Notificaciones;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Push;

namespace UltimaMilla.Infrastructure.Push;

// Cuando el Backoffice asigna o reasigna un envío a una ruta, avisa al celular del repartidor de esa ruta con
// Web Push. El service worker de la PWA lo muestra aunque la app esté cerrada (docs/push-repartidor.md).
// - Idempotente con su clave (MensajeId, "push-repartidor"): si después fallan los webhooks, no se repite.
// - Fallo transitorio del servicio push => excepción y reintento por las colas de espera.
// - Suscripción vencida (404/410) => se borra; no se reintenta.
// La ruta y el repartidor son del operador, no de la cuenta del mensaje: se leen con un contexto de operador
// (el mismo criterio que RegistroIdempotencia), con el operador ya verificado del mensaje.
public sealed class AvisoPushRepartidor(DbContextOptions<AppDbContext> opciones, IRegistroIdempotencia idempotencia,
    IClienteWebPush cliente, OpcionesWebPush configuracion, ILogger<AvisoPushRepartidor> logger) : IManejadorEventoEnvio
{
    public const string Consumidor = "push-repartidor";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task ManejarAsync(EventoEnvioV1 evento, TenantContext tenant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(tenant);
        if (!MensajesPushRepartidor.EsRelevante(evento.TipoEvento)) return;
        if (!configuracion.PuedeEnviar)
        {
            logger.LogDebug("Web Push sin claves VAPID: no se avisa al repartidor ({EventoId}).", evento.EventoId);
            return;
        }
        await idempotencia.EjecutarUnaVezAsync(tenant, evento.MensajeId, Consumidor,
            ct => AvisarAsync(evento, tenant.OperadorId, ct), cancellationToken);
    }

    private async Task AvisarAsync(EventoEnvioV1 evento, Guid operadorId, CancellationToken cancellationToken)
    {
        await using var db = new AppDbContext(opciones, TenantActual.ParaOperador(operadorId));
        // La parada activa del envío dice a qué ruta (y repartidor) quedó asignado ahora.
        var parada = await db.Set<Parada>().AsNoTracking()
            .Where(p => p.Envio.Id == evento.EnvioId && p.Activa)
            .Select(p => new { RepartidorId = p.Ruta.Repartidor.Id, p.Cod, p.HoraEstimadaLlegada, p.Envio.CodigoSeguimiento })
            .SingleOrDefaultAsync(cancellationToken);
        if (parada is null)
        {
            logger.LogInformation("El envío {EnvioId} ya no tiene parada activa; no se avisa ({EventoId}).", evento.EnvioId, evento.EventoId);
            return;
        }

        var suscripciones = await db.Set<SuscripcionPush>()
            .Where(s => s.Usuario.Id == parada.RepartidorId).ToListAsync(cancellationToken);
        if (suscripciones.Count == 0)
        {
            logger.LogInformation("El repartidor {RepartidorId} no activó los avisos push ({EventoId}).", parada.RepartidorId, evento.EventoId);
            return;
        }

        var aviso = MensajesPushRepartidor.Componer(evento.TipoEvento, evento.EnvioId, parada.CodigoSeguimiento,
            parada.Cod, parada.HoraEstimadaLlegada);
        var contenido = JsonSerializer.SerializeToUtf8Bytes(aviso, Json);
        var entregados = 0;
        foreach (var suscripcion in suscripciones)
        {
            var resultado = await cliente.EnviarAsync(new Uri(suscripcion.Endpoint), suscripcion.P256dh, suscripcion.Auth,
                contenido, aviso.Etiqueta, cancellationToken);
            if (resultado == ResultadoPush.Entregado) entregados++;
            else if (resultado == ResultadoPush.Vencida) db.Remove(suscripcion);
            else logger.LogWarning("El servicio push rechazó el aviso para la suscripción {SuscripcionId}.", suscripcion.Id);
        }
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Aviso push {Tipo} del envío {EnvioId}: {Entregados} de {Total} dispositivos ({EventoId}).",
            evento.TipoEvento, evento.EnvioId, entregados, suscripciones.Count, evento.EventoId);
    }
}
