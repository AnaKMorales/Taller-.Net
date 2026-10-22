using UltimaMilla.Application.Tarifas;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Cache;

// Perfil "configuración": duración larga e invalidación explícita al cambiar el tarifario del operador.
public sealed class TarifarioVigenteCacheado(ITarifarioVigente origen, CacheSegura cache, CacheOpciones opciones,
    TimeProvider reloj) : ITarifarioVigente, IInvalidacionTarifario
{
    public const string NombreCache = "tarifario";

    // El tarifario es del operador, no de un usuario: la clave lleva solo el prefijo del operador.
    public static string Clave(Guid operadorId) => TenantCacheKey.PrefijoOperador(operadorId) + "tarifario:vigente";

    public Task<TarifarioDto?> ObtenerAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
        cache.ObtenerOCrearAsync(NombreCache, Clave(operadorId),
            ct => origen.ObtenerAsync(operadorId, ct), Duracion, cancellationToken);

    public Task InvalidarAsync(Guid operadorId, CancellationToken cancellationToken = default) =>
        cache.QuitarAsync(NombreCache, Clave(operadorId), cancellationToken);

    private TimeSpan Duracion(TarifarioDto tarifario)
    {
        var duracion = opciones.DuracionTarifario;
        // Si ya hay otro cuadro cargado para el futuro, la entrada vence justo cuando ese entra en vigor.
        if (tarifario.ProximaVigencia is { } proxima)
        {
            var hastaProxima = proxima - reloj.GetUtcNow().UtcDateTime;
            if (hastaProxima < duracion) duracion = hastaProxima;
        }
        return duracion;
    }
}
