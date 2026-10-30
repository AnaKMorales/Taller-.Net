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
            var payload = new EventoEnvioV1(evento.Id, 1, db.OperadorIdActual, cuentaId,
                evento.EnvioId, evento.Id, evento.Tipo.ToString(), evento.Origen.ToString(),
                evento.EstadoAnterior?.ToString(), evento.EstadoNuevo.ToString(), evento.OcurridoEn,
                evento.IdOperacion);
            db.Add(new MensajeOutbox(evento, JsonSerializer.Serialize(payload, JsonOptions)));
        }
    }
}
