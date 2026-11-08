using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Outbox;

internal static class OutboxPreparation
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static void Prepare(AppDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        var pending = db.ChangeTracker.Entries<MensajeOutbox>()
            .Where(e => e.State == EntityState.Added).Select(e => e.Entity.Id).ToHashSet();
        foreach (var entry in db.ChangeTracker.Entries<EventoDeEnvio>()
                     .Where(e => e.State == EntityState.Added).ToArray())
        {
            var evento = entry.Entity;
            if (!pending.Add(evento.Id)) continue;
            // Se lee la FK de EF: no exige cargar la navegación CuentaComercial.
            var cuentaId = db.Entry(evento.Envio).Property<Guid>("CuentaComercialKey").CurrentValue;
            var intento = evento.IdOperacion is Guid operacion ? evento.Envio.Intentos
                .SingleOrDefault(i => i.IdOperacion == operacion && i.HuellaSolicitud is not null) : null;
            if (intento is not null || evento.EstadoAnterior == EstadoEnvio.EntregadoParcialmente ||
                evento.EstadoNuevo == EstadoEnvio.EntregadoParcialmente)
            {
                var v2 = new EventoEnvioV2(evento.Id, EventoEnvioV2.VersionContrato, db.OperadorIdActual, cuentaId, evento.EnvioId,
                    evento.Id, evento.Tipo.ToString(), evento.Origen.ToString(), evento.EstadoAnterior?.ToString(),
                    evento.EstadoNuevo.ToString(), evento.OcurridoEn, intento?.IdOperacion,
                    intento is null ? [] : intento.Detalles.OrderBy(d => d.Bulto.Id).Select(d => new ResultadoBultoV2(d.Bulto.Id,
                        d.Resultado.ToString(), d.Motivo?.Id, d.Evidencias.Select(p => p.Prueba.Id).Order().ToArray())).ToArray());
                db.Add(new MensajeOutbox(evento, JsonSerializer.Serialize(v2, JsonOptions), EventoEnvioV2.TipoMensaje));
                continue;
            }
            var payload = new EventoEnvioV1(evento.Id, EventoEnvioV1.VersionContrato, db.OperadorIdActual, cuentaId,
                evento.EnvioId, evento.Id, evento.Tipo.ToString(), evento.Origen.ToString(),
                evento.EstadoAnterior?.ToString(), evento.EstadoNuevo.ToString(), evento.OcurridoEn,
                evento.IdOperacion);
            db.Add(new MensajeOutbox(evento, JsonSerializer.Serialize(payload, JsonOptions)));
        }
    }
}
