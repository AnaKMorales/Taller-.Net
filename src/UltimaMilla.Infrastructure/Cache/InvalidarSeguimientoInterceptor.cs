using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace UltimaMilla.Infrastructure.Cache;

// Invalidación por evento del perfil "seguimiento"
public sealed class InvalidarSeguimientoInterceptor(CacheSegura cache) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, HashSet<string>> _pendientes = new();

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
        InvalidarAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        await InvalidarAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Descartar(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Descartar(eventData.Context);
        return Task.CompletedTask;
    }

    // Se anota antes de guardar (después, el ChangeTracker ya no distingue qué cambió).
    // Cuenta como cambio: un evento nuevo, el envío modificado o un bulto modificado (entrega parcial).
    private void Anotar(DbContext? context)
    {
        if (context is null) return;
        var tracker = context.ChangeTracker;
        var codigos = tracker.Entries<EventoDeEnvio>().Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity.Envio.CodigoSeguimiento)
            .Concat(tracker.Entries<Envio>().Where(e => e.State == EntityState.Modified)
                .Select(e => e.Entity.CodigoSeguimiento))
            .Concat(tracker.Entries<Bulto>().Where(e => e.State == EntityState.Modified && e.Entity.Envio is not null)
                .Select(e => e.Entity.Envio!.CodigoSeguimiento))
            .ToHashSet();
        if (codigos.Count > 0) _pendientes.AddOrUpdate(context, codigos);
    }

    private void Descartar(DbContext? context)
    {
        if (context is not null) _pendientes.Remove(context);
    }

    // Solo después de confirmar el guardado: si se invalidara antes, una consulta concurrente podría
    // volver a cachear el estado viejo. Los errores de la caché los absorbe CacheSegura.QuitarAsync.
    private async Task InvalidarAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context is null || !_pendientes.TryGetValue(context, out var codigos)) return;
        _pendientes.Remove(context);
        foreach (var codigo in codigos)
        {
            if (Guid.TryParseExact(codigo, "N", out var token))
                await cache.QuitarAsync(SeguimientoPublicoCacheado.NombreCache, SeguimientoPublicoCacheado.Clave(token),
                    cancellationToken);
        }
    }
}
