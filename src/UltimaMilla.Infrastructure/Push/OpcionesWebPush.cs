using System.Security.Cryptography;

namespace UltimaMilla.Infrastructure.Push;

// Claves VAPID (RFC 8292) en base64url, el mismo formato que "npx web-push generate-vapid-keys" o
// scripts/generar-claves-webpush.sh. La privada es secreta: solo la usa el Worker (variable de entorno / SSM).
public sealed class OpcionesWebPush
{
    public const string Seccion = "WebPush";

    public string? ClavePublica { get; set; }
    public string? ClavePrivada { get; set; }
    // Contacto para el servicio push si hay problemas con nuestros envíos (mailto: o https:).
    public string Asunto { get; set; } = "mailto:soporte@ultimamilla.local";
    // Cuánto guarda el servicio push el aviso si el celular está apagado o sin señal.
    public TimeSpan TiempoDeVida { get; set; } = TimeSpan.FromHours(12);
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
    // Solo se envía a estos servicios push (evita que un endpoint inventado haga que el Worker llame a otro sitio).
    public string[] ServiciosPermitidos { get; set; } =
    [
        "fcm.googleapis.com", "android.googleapis.com", "updates.push.services.mozilla.com",
        "web.push.apple.com", "*.push.apple.com", "*.notify.windows.com",
    ];

    public bool PuedeSuscribir => !string.IsNullOrWhiteSpace(ClavePublica);
    public bool PuedeEnviar => PuedeSuscribir && !string.IsNullOrWhiteSpace(ClavePrivada);

    public bool EsServicioPermitido(Uri endpoint) =>
        endpoint is { IsAbsoluteUri: true, Scheme: "https", IsDefaultPort: true, UserInfo: "" } &&
        ServiciosPermitidos.Any(s => s.StartsWith("*.", StringComparison.Ordinal)
            ? endpoint.IdnHost.EndsWith(s[1..], StringComparison.OrdinalIgnoreCase)
            : endpoint.IdnHost.Equals(s, StringComparison.OrdinalIgnoreCase));

    // Sin clave pública, Web Push queda apagado (la PWA lo informa). Con pública pero sin privada (Web), solo se
    // aceptan suscripciones. Con las dos (Worker), se comprueba que formen un par.
    public void Validar()
    {
        if (!PuedeSuscribir) return;
        if (!Base64Url.Intentar(ClavePublica, out var publica) || publica is not { Length: 65 } || publica[0] != 0x04)
            throw new InvalidOperationException("WebPush:ClavePublica debe ser un punto P-256 sin comprimir en base64url.");
        if (Timeout <= TimeSpan.Zero || TiempoDeVida <= TimeSpan.Zero)
            throw new InvalidOperationException("WebPush: Timeout y TiempoDeVida deben ser positivos.");
        if (!Asunto.StartsWith("mailto:", StringComparison.Ordinal) && !Asunto.StartsWith("https://", StringComparison.Ordinal))
            throw new InvalidOperationException("WebPush:Asunto debe empezar con mailto: o https://.");
        if (string.IsNullOrWhiteSpace(ClavePrivada)) return;
        bool coinciden;
        try
        {
            using var clave = CrearFirmante();
            // Se firma y se verifica con la pública configurada: si no son par, la verificación falla.
            using var verificador = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = publica[1..33], Y = publica[33..65] },
            });
            var prueba = "ultimamilla"u8.ToArray();
            coinciden = verificador.VerifyData(prueba, clave.SignData(prueba, HashAlgorithmName.SHA256), HashAlgorithmName.SHA256);
        }
        catch (CryptographicException) { coinciden = false; }
        if (!coinciden) throw new InvalidOperationException("WebPush:ClavePrivada no corresponde a WebPush:ClavePublica.");
    }

    internal ECDsa CrearFirmante()
    {
        if (!Base64Url.Intentar(ClavePrivada, out var d) || d.Length != 32)
            throw new InvalidOperationException("WebPush:ClavePrivada debe tener 32 bytes en base64url.");
        // Solo con la privada: .NET calcula el punto público. Así una pública equivocada no se cuela en la firma.
        var clave = ECDsa.Create();
        clave.ImportParameters(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = d });
        return clave;
    }
}
