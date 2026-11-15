using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UltimaMilla.Application.Avisos;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Avisos;

// Envía el webhook de un evento a cada SuscripcionAviso activa de la cuenta del evento cuyo tipo coincida.
// - Cada intento queda en EntregaAviso (se guarda uno por uno, aunque después falle otro).
// - Una entrega ya exitosa no se vuelve a enviar: el reintento solo toca las que fallaron.
// - Un fallo no frena a las otras suscripciones; al final se lanza AvisoWebhookFallidoException para que
//   el mensaje vaya a la cola de espera. Aquí no se reintenta: el siguiente intento llega por la cola.
// Corre con el TenantContext del mensaje (con cuenta): solo ve y escribe datos de esa cuenta.
public sealed class EntregaWebhooks(DbContextOptions<AppDbContext> opciones, IClienteWebhook cliente, TimeProvider reloj,
    ILogger<EntregaWebhooks> logger) : IManejadorEventoEnvio
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task ManejarAsync(EventoEnvioV1 evento, TenantContext tenant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(tenant);
        if (!Enum.TryParse<TipoEvento>(evento.TipoEvento, ignoreCase: false, out var tipo) || !Enum.IsDefined(tipo))
        {
            logger.LogWarning("Evento {EventoId} con tipo desconocido; no hay webhooks que enviar.", evento.EventoId);
            return;
        }

        await using var db = new AppDbContext(opciones, new TenantActual(tenant));
        // El dominio navega Envio -> CuentaComercial (y Suscripcion -> CuentaComercial): hay que cargarlas.
        var dominio = await db.Set<EventoDeEnvio>().Include(e => e.Envio).ThenInclude(x => x.CuentaComercial)
            .SingleOrDefaultAsync(e => e.Id == evento.EventoId, cancellationToken);
        if (dominio is null)
        {
            logger.LogWarning("Evento {EventoId} no encontrado para el operador; no se envían webhooks.", evento.EventoId);
            return;
        }

        var cuentaId = dominio.Envio.CuentaComercialId;
        var activas = await db.Set<SuscripcionAviso>().Include(s => s.CuentaComercial)
            .Where(s => s.Activa && EF.Property<Guid>(s, "CuentaComercialKey") == cuentaId)
            .ToListAsync(cancellationToken);
        var coinciden = activas.Where(s => s.TiposEvento.Contains(tipo)).ToList();
        if (coinciden.Count == 0) return;

        var existentes = await db.Set<EntregaAviso>().Include(e => e.Suscripcion)
            .Where(e => EF.Property<Guid>(e, "EventoKey") == evento.EventoId).ToListAsync(cancellationToken);
        var cuerpoV1 = JsonSerializer.Serialize(evento, Json);
        string? cuerpoV2 = null;
        if (coinciden.Any(s => s.VersionContrato == 2))
        {
            // El intento y sus evidencias son históricos e inmutables. No se lee el estado
            // actual de los bultos: un reintento o reenvío manual debe conservar el resultado original.
            var intento = dominio.IdOperacion is Guid operacion
                ? await db.Set<IntentoEntrega>().AsNoTracking()
                    .Include(i => i.Detalles).ThenInclude(d => d.Bulto)
                    .Include(i => i.Detalles).ThenInclude(d => d.Motivo)
                    .Include(i => i.Detalles).ThenInclude(d => d.Evidencias).ThenInclude(p => p.Prueba)
                    .SingleOrDefaultAsync(i => i.Envio.Id == dominio.EnvioId && i.IdOperacion == operacion &&
                        i.HuellaSolicitud != null, cancellationToken)
                : null;
            var v2 = new EventoEnvioV2(evento.MensajeId, 2, tenant.OperadorId, cuentaId, dominio.EnvioId,
                dominio.Id, dominio.Tipo.ToString(), dominio.Origen.ToString(), dominio.EstadoAnterior?.ToString(),
                dominio.EstadoNuevo.ToString(), dominio.OcurridoEn, intento?.IdOperacion,
                intento is null ? [] : intento.Detalles.OrderBy(d => d.Bulto.Id).Select(d => new ResultadoBultoV2(
                    d.Bulto.Id, d.Resultado.ToString(), d.Motivo?.Id,
                    d.Evidencias.Select(p => p.Prueba.Id).Order().ToArray())).ToArray());
            cuerpoV2 = JsonSerializer.Serialize(v2, Json);
        }
        var fallidas = new List<Guid>();

        foreach (var suscripcion in coinciden)
        {
            var entrega = existentes.FirstOrDefault(e => e.SuscripcionId == suscripcion.Id);
            if (entrega is { Exitoso: true }) continue; // ya entregado en un intento anterior

            if (entrega is null)
            {
                entrega = new EntregaAviso(suscripcion, dominio);
                db.Add(entrega);
            }

            var ahora = reloj.GetUtcNow();
            ResultadoWebhook resultado;
            var cuerpo = suscripcion.VersionContrato == 2 ? cuerpoV2! : cuerpoV1;
            try { resultado = await cliente.EnviarAsync(new SolicitudWebhook(suscripcion.UrlDestino, suscripcion.SecretoFirma, cuerpo, evento.EventoId, ahora), cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                logger.LogError(error, "Error inesperado enviando el webhook {SuscripcionId}.", suscripcion.Id);
                resultado = ResultadoWebhook.Fallo("Error inesperado");
            }

            // La regla de dominio exige que el intento no sea anterior al evento ni al intento previo.
            var momento = new[] { ahora.UtcDateTime, dominio.OcurridoEn, entrega.UltimoIntentoEn ?? DateTime.MinValue }.Max();
            entrega.RegistrarIntento(resultado.Exitoso, momento);
            await db.SaveChangesAsync(cancellationToken);

            if (resultado.Exitoso)
                logger.LogInformation("Webhook {SuscripcionId} del evento {EventoId} entregado (HTTP {Codigo}).", suscripcion.Id, evento.EventoId, resultado.CodigoHttp);
            else
            {
                // Sin URL ni cuerpo en el log: pueden llevar datos del comercio.
                logger.LogWarning("Webhook {SuscripcionId} del evento {EventoId} falló (intento {Intento}): {Motivo}.",
                    suscripcion.Id, evento.EventoId, entrega.Intentos, resultado.Motivo);
                fallidas.Add(suscripcion.Id);
            }
        }

        if (fallidas.Count > 0) throw new AvisoWebhookFallidoException(evento.EventoId, fallidas);
    }
}
