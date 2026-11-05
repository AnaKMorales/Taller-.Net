using System.Net;
using System.Net.Sockets;

namespace UltimaMilla.Infrastructure.Avisos;

// La URL del webhook la define el comercio: sin este control podría hacer que el servidor consulte su red interna
// (localhost, metadatos de la nube, bases de datos...). Se valida la IP a la que realmente se conecta.
public static class DestinoSeguro
{
    public static bool EsPublica(IPAddress direccion)
    {
        ArgumentNullException.ThrowIfNull(direccion);
        if (direccion.IsIPv4MappedToIPv6) direccion = direccion.MapToIPv4();
        if (IPAddress.IsLoopback(direccion) || direccion.Equals(IPAddress.Any) || direccion.Equals(IPAddress.IPv6Any) ||
            direccion.Equals(IPAddress.Broadcast)) return false;

        var b = direccion.GetAddressBytes();
        if (direccion.AddressFamily == AddressFamily.InterNetwork)
            return !(b[0] == 0 ||                                   // 0.0.0.0/8
                     b[0] == 10 ||                                  // 10.0.0.0/8
                     (b[0] == 100 && b[1] is >= 64 and <= 127) ||   // 100.64.0.0/10 (CGNAT)
                     (b[0] == 169 && b[1] == 254) ||                // 169.254.0.0/16 (enlace local, metadatos de la nube)
                     (b[0] == 172 && b[1] is >= 16 and <= 31) ||    // 172.16.0.0/12
                     (b[0] == 192 && b[1] == 168) ||                // 192.168.0.0/16
                     (b[0] == 192 && b[1] == 0 && b[2] == 0) ||     // 192.0.0.0/24
                     (b[0] == 198 && b[1] is 18 or 19) ||           // 198.18.0.0/15
                     b[0] >= 224);                                  // multicast y reservadas

        if (direccion.AddressFamily == AddressFamily.InterNetworkV6)
            return !(direccion.IsIPv6LinkLocal || direccion.IsIPv6SiteLocal || direccion.IsIPv6Multicast ||
                     (b[0] & 0xFE) == 0xFC);                        // fc00::/7 (unique local)

        return false;
    }
}
