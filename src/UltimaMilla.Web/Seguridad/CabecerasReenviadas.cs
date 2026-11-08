using Microsoft.AspNetCore.HttpOverrides;

namespace UltimaMilla.Web.Seguridad;

/// <summary>
/// Detrás de Traefik (que termina el TLS), Web recibe HTTP y la IP del proxy. Con esto toma el esquema, host e IP
/// reales de X-Forwarded-*, pero solo si el pedido viene de una red confiable.
/// </summary>
public static class CabecerasReenviadas
{
    /// <summary>Clave de configuración: redes (CIDR) separadas por coma o punto y coma, p. ej. "172.28.0.0/16".</summary>
    public const string ClaveRedConfiable = "ForwardedHeaders:RedConfiable";

    public static IServiceCollection AgregarCabecerasReenviadas(this IServiceCollection servicios)
    {
        // La configuración se lee al construir las opciones (no al armar el builder), así toma también los overrides del host.
        servicios.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((o, configuracion) =>
        {
            var redes = LeerRedes(configuracion[ClaveRedConfiable]);

            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

            // Sin configuración queda lo de fábrica de ASP.NET: solo loopback (127.0.0.1 y ::1).
            if (redes.Count == 0)
                return;

            o.KnownProxies.Clear();
            o.KnownIPNetworks.Clear();
            foreach (var red in redes)
                o.KnownIPNetworks.Add(red);
        });

        return servicios;
    }

    public static IReadOnlyList<System.Net.IPNetwork> LeerRedes(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return [];

        var redes = new List<System.Net.IPNetwork>();
        foreach (var texto in valor.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!System.Net.IPNetwork.TryParse(texto, out var red))
                throw new FormatException($"{ClaveRedConfiable}: '{texto}' no es un CIDR válido (ejemplo: 172.28.0.0/16).");
            redes.Add(red);
        }

        return redes;
    }
}
