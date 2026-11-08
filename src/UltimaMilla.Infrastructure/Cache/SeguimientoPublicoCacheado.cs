using System.Security.Cryptography;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Cache;

// Perfil "seguimiento": duración corta e invalidación por evento (InvalidarSeguimientoInterceptor). Es la consulta pública más frecuente
// y la que más se beneficia, porque un destinatario recarga la página varias veces.
public sealed class SeguimientoPublicoCacheado(ISeguimientoPublico origen, CacheSegura cache, CacheOpciones opciones)
    : ISeguimientoPublico
{
    public const string NombreCache = "seguimiento";

    // Se guarda un hash del token, no el token: con las claves de Redis no se pueden consultar envíos.
    // El token es aleatorio y único en toda la plataforma, así que la clave no puede cruzar operadores.
    public static string Clave(Guid token) =>
        "seguimiento:" + Convert.ToHexStringLower(SHA256.HashData(token.ToByteArray()));

    public Task<SeguimientoPublicoDto?> ConsultarAsync(string token, CancellationToken cancellationToken = default)
    {
        // Un token mal formado no ocupa la caché ni cuenta en la métrica; el origen ya lo rechaza.
        if (!Guid.TryParseExact(token, "N", out var parsed)) return origen.ConsultarAsync(token, cancellationToken);
        var normalizado = parsed.ToString("N");
        return cache.ObtenerOCrearAsync(NombreCache, Clave(parsed),
            ct => origen.ConsultarAsync(normalizado, ct), _ => opciones.DuracionSeguimiento, cancellationToken);
    }
}
