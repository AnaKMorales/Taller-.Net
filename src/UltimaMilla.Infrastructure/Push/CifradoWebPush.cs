using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace UltimaMilla.Infrastructure.Push;

// Cifrado del contenido de Web Push (RFC 8291 sobre "aes128gcm", RFC 8188). Solo el navegador del repartidor
// puede leerlo: el servicio push intermedio ve bytes opacos.
public static class CifradoWebPush
{
    private const int TamanoRegistro = 4096;
    // Un solo registro: contenido + delimitador (1) + etiqueta GCM (16) tiene que entrar en TamanoRegistro.
    public const int MaximoContenido = TamanoRegistro - 1 - 16 - 86;

    public static byte[] Cifrar(ReadOnlySpan<byte> contenido, string p256dh, string auth) =>
        Cifrar(contenido, Base64Url.Decodificar(p256dh), Base64Url.Decodificar(auth),
            ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256), RandomNumberGenerator.GetBytes(16));

    // Con la clave efímera y el salt explícitos para poder verificarlo contra el vector del RFC 8291 (apéndice A).
    // En producción se usa la sobrecarga de arriba, que los genera al azar en cada aviso. Toma posesión de "efimera".
    public static byte[] Cifrar(ReadOnlySpan<byte> contenido, byte[] clavePublicaDispositivo, byte[] secretoAuth,
        ECDiffieHellman efimera, byte[] salt)
    {
        if (contenido.Length > MaximoContenido) throw new ArgumentException("El aviso es demasiado grande.", nameof(contenido));
        if (clavePublicaDispositivo is not { Length: 65 } || clavePublicaDispositivo[0] != 0x04)
            throw new ArgumentException("La clave p256dh no es un punto P-256 sin comprimir.", nameof(clavePublicaDispositivo));
        if (secretoAuth is not { Length: 16 }) throw new ArgumentException("El secreto auth debe tener 16 bytes.", nameof(secretoAuth));
        if (salt is not { Length: 16 }) throw new ArgumentException("El salt debe tener 16 bytes.", nameof(salt));

        using var dispositivo = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = clavePublicaDispositivo[1..33], Y = clavePublicaDispositivo[33..65] },
        });
        using (efimera)
        {
            var publicaServidor = PuntoSinComprimir(efimera.ExportParameters(false));
            var secretoEcdh = efimera.DeriveRawSecretAgreement(dispositivo.PublicKey);

            // IKM = HKDF(auth, ecdh, "WebPush: info" || 0x00 || clave del dispositivo || clave del servidor, 32)
            var infoClave = Concatenar(Encoding.ASCII.GetBytes("WebPush: info\0"), clavePublicaDispositivo, publicaServidor);
            var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, secretoEcdh, 32, secretoAuth, infoClave);
            var prk = HKDF.Extract(HashAlgorithmName.SHA256, ikm, salt);
            var cek = HKDF.Expand(HashAlgorithmName.SHA256, prk, 16, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
            var nonce = HKDF.Expand(HashAlgorithmName.SHA256, prk, 12, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

            // Encabezado: salt (16) || tamaño de registro (4) || largo de la clave (1) || clave pública del servidor (65)
            var salida = new byte[16 + 4 + 1 + 65 + contenido.Length + 1 + 16];
            salt.CopyTo(salida, 0);
            BinaryPrimitives.WriteUInt32BigEndian(salida.AsSpan(16, 4), TamanoRegistro);
            salida[20] = 65;
            publicaServidor.CopyTo(salida, 21);

            var claro = new byte[contenido.Length + 1];
            contenido.CopyTo(claro);
            claro[^1] = 0x02; // delimitador del último (y único) registro
            using var aes = new AesGcm(cek, 16);
            aes.Encrypt(nonce, claro, salida.AsSpan(86, claro.Length), salida.AsSpan(86 + claro.Length, 16));
            return salida;
        }
    }

    internal static byte[] PuntoSinComprimir(ECParameters parametros) =>
        Concatenar([0x04], parametros.Q.X!, parametros.Q.Y!);

    private static byte[] Concatenar(params byte[][] partes)
    {
        var resultado = new byte[partes.Sum(p => p.Length)];
        var posicion = 0;
        foreach (var parte in partes) { parte.CopyTo(resultado, posicion); posicion += parte.Length; }
        return resultado;
    }
}
