using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace UltimaMilla.Infrastructure.Cache;

// Patrón cache-aside sobre IDistributedCache (Redis, o memoria si no hay Redis configurado).
// Si la caché falla, se registra el error y se lee de la base: la caché nunca debe tirar abajo una consulta.
public sealed class CacheSegura(IDistributedCache cache, CacheMetricas metricas, ILogger<CacheSegura> logger,
    TimeProvider reloj)
{
    // Tras una falla no se vuelve a probar Redis durante este tiempo: cada intento contra un Redis caído
    // demora segundos, y es preferible ir directo a la base hasta que vuelva.
    public static readonly TimeSpan PausaTrasFalla = TimeSpan.FromSeconds(30);

    private long _pausadaHasta;

    public async Task<T?> ObtenerOCrearAsync<T>(string nombreCache, string clave,
        Func<CancellationToken, Task<T?>> leerOrigen, Func<T, TimeSpan> duracion,
        CancellationToken cancellationToken = default) where T : class
    {
        if (Pausada()) return await leerOrigen(cancellationToken);

        try
        {
            var guardado = await cache.GetAsync(clave, cancellationToken);
            if (guardado is not null && JsonSerializer.Deserialize<T>(guardado) is { } valor)
            {
                metricas.Acierto(nombreCache);
                return valor;
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Fallo(nombreCache, error, "No se pudo leer la caché {Cache}; se consulta la base.");
            return await leerOrigen(cancellationToken);
        }

        metricas.Fallo(nombreCache);
        var leido = await leerOrigen(cancellationToken);
        // No se guarda "no existe": así un envío o tarifario recién creado se ve enseguida.
        if (leido is null) return null;

        var expira = duracion(leido);
        if (expira <= TimeSpan.Zero) return leido;
        try
        {
            await cache.SetAsync(clave, JsonSerializer.SerializeToUtf8Bytes(leido),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expira }, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Fallo(nombreCache, error, "No se pudo guardar en la caché {Cache}.");
        }
        return leido;
    }

    public async Task QuitarAsync(string nombreCache, string clave, CancellationToken cancellationToken = default)
    {
        // Se intenta aunque la caché esté en pausa: una entrada vieja que sobreviva es peor que una demora.
        try
        {
            await cache.RemoveAsync(clave, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // Si no se pudo invalidar, la entrada vieja vive como máximo su duración configurada.
            Fallo(nombreCache, error, "No se pudo invalidar la caché {Cache}.");
        }
    }

    private bool Pausada() => reloj.GetUtcNow().UtcTicks < Interlocked.Read(ref _pausadaHasta);

    private void Fallo(string nombreCache, Exception error, string mensaje)
    {
        metricas.Error(nombreCache);
        Interlocked.Exchange(ref _pausadaHasta, reloj.GetUtcNow().Add(PausaTrasFalla).UtcTicks);
        logger.LogWarning(error, mensaje, nombreCache);
    }
}
