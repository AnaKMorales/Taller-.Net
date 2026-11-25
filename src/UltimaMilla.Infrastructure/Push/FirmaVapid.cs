using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UltimaMilla.Infrastructure.Push;

// Encabezado Authorization de VAPID (RFC 8292): un JWT ES256 que prueba ante el servicio push que el aviso
// sale del dueño de la clave pública con la que se suscribió el dispositivo.
public static class FirmaVapid
{
    public static string Encabezado(Uri endpoint, OpcionesWebPush opciones, DateTimeOffset ahora)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(opciones);
        var audiencia = endpoint.GetLeftPart(UriPartial.Authority);
        var cabecera = Base64Url.Codificar("""{"typ":"JWT","alg":"ES256"}"""u8);
        var reclamos = Base64Url.Codificar(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["aud"] = audiencia,
            // Máximo 24 h según el RFC; 12 h deja margen para relojes desfasados.
            ["exp"] = ahora.AddHours(12).ToUnixTimeSeconds(),
            ["sub"] = opciones.Asunto,
        }));
        var firmable = Encoding.ASCII.GetBytes($"{cabecera}.{reclamos}");
        using var clave = opciones.CrearFirmante();
        // .NET firma en formato IEEE P1363 (r || s de 32 bytes cada uno): el que exige JWS para ES256.
        var firma = clave.SignData(firmable, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"vapid t={cabecera}.{reclamos}.{Base64Url.Codificar(firma)}, k={opciones.ClavePublica}";
    }
}
