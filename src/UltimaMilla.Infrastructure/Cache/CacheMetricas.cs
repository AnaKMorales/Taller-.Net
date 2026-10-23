using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace UltimaMilla.Infrastructure.Cache;

public sealed record ResumenCache(string Cache, long Aciertos, long Fallos, long Errores, double TasaAciertos);

// Cuenta aciertos, fallos y errores por caché. Publica los contadores en el Meter "UltimaMilla.Cache"
// para que OpenTelemetry/Prometheus los recojan, y guarda un resumen en memoria para consultarlo sin ese stack.
public sealed class CacheMetricas : IDisposable
{
    public const string NombreMeter = "UltimaMilla.Cache";

    private readonly Meter _meter = new(NombreMeter);
    private readonly Counter<long> _aciertos;
    private readonly Counter<long> _fallos;
    private readonly Counter<long> _errores;
    private readonly ConcurrentDictionary<string, Contadores> _porCache = new();

    public CacheMetricas()
    {
        _aciertos = _meter.CreateCounter<long>("ultimamilla.cache.aciertos", description: "Lecturas resueltas desde la caché");
        _fallos = _meter.CreateCounter<long>("ultimamilla.cache.fallos", description: "Lecturas que tuvieron que ir a la base");
        _errores = _meter.CreateCounter<long>("ultimamilla.cache.errores", description: "Fallas al leer o escribir en la caché");
    }

    public void Acierto(string cache)
    {
        _aciertos.Add(1, new KeyValuePair<string, object?>("cache", cache));
        Interlocked.Increment(ref Contador(cache).Aciertos);
    }

    public void Fallo(string cache)
    {
        _fallos.Add(1, new KeyValuePair<string, object?>("cache", cache));
        Interlocked.Increment(ref Contador(cache).Fallos);
    }

    public void Error(string cache)
    {
        _errores.Add(1, new KeyValuePair<string, object?>("cache", cache));
        Interlocked.Increment(ref Contador(cache).Errores);
    }

    public IReadOnlyList<ResumenCache> Resumen() => _porCache
        .OrderBy(p => p.Key)
        .Select(p =>
        {
            var aciertos = Interlocked.Read(ref p.Value.Aciertos);
            var fallos = Interlocked.Read(ref p.Value.Fallos);
            var lecturas = aciertos + fallos;
            return new ResumenCache(p.Key, aciertos, fallos, Interlocked.Read(ref p.Value.Errores),
                lecturas == 0 ? 0 : Math.Round((double)aciertos / lecturas, 4));
        })
        .ToList();

    public void Dispose() => _meter.Dispose();

    private Contadores Contador(string cache) => _porCache.GetOrAdd(cache, _ => new Contadores());

    private sealed class Contadores
    {
        public long Aciertos;
        public long Fallos;
        public long Errores;
    }
}
