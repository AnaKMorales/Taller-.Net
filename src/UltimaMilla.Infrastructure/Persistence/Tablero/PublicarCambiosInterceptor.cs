using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using UltimaMilla.Application.Tablero;

namespace UltimaMilla.Infrastructure.Persistence.Tablero;

// Cada EventoDeEnvio nuevo es un cambio de estado. Se anotan al guardar y se publican al tablero
// solo si el guardado se confirmó; si falla, se descartan.
// Limitación: con una transacción explícita se publica antes del commit. El outbox (ADR-006) lo resuelve.
public sealed class PublicarCambiosInterceptor(INotificadorTablero notificador, ILogger<PublicarCambiosInterceptor> logger)
    : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<CambioEnvio>> _pendientes = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Anotar(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Anotar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        PublicarAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await PublicarAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Descartar(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Descartar(eventData.Context);
        return Task.CompletedTask;
    }

    private void Anotar(DbContext? context)
    {
        // Sin operador no se puede escribir (lo bloquea el interceptor de tenant), así que no hay nada que avisar.
        if (context is not AppDbContext db || db.OperadorIdActual == Guid.Empty) return;
        var cambios = db.ChangeTracker.Entries<EventoDeEnvio>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .OrderBy(e => e.OcurridoEn)
            .Select(e => new CambioEnvio(db.OperadorIdActual, e.Envio.Id, e.Tipo, e.EstadoAnterior, e.EstadoNuevo,
                e.OcurridoEn, e.Detalle))
            .ToList();
        if (cambios.Count > 0) _pendientes.AddOrUpdate(db, cambios);
    }

    private void Descartar(DbContext? context)
    {
        if (context is not null) _pendientes.Remove(context);
    }

    private async Task PublicarAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || !_pendientes.TryGetValue(context, out var cambios)) return;
        _pendientes.Remove(context);
        try
        {
            await notificador.PublicarAsync(cambios, cancellationToken);
        }
        catch (Exception error)
        {
            // Los datos ya se guardaron: si el aviso en vivo falla, el tablero se pone al día al recargar.
            logger.LogWarning(error, "No se pudieron publicar {Cantidad} cambios al tablero.", cambios.Count);
        }
    }
}
