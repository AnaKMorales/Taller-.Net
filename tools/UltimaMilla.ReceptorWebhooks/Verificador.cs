using System.Security.Cryptography;
using System.Text;

namespace UltimaMilla.ReceptorWebhooks;

// Verificación de la firma hecha por el "comercio", sin usar código de UltimaMilla:
//   X-Signature = "sha256=" + hex(HMAC-SHA256(secreto, "{X-Timestamp}.{cuerpo}"))
public sealed class Verificador(string secreto, TimeSpan tolerancia, TimeProvider reloj)
{
    public ResultadoVerificacion Verificar(string? timestamp, string? firma, string cuerpo)
    {
        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(firma)) return ResultadoVerificacion.Faltan;
        if (!long.TryParse(timestamp, out var segundos)) return ResultadoVerificacion.TimestampInvalido;
        if ((reloj.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(segundos)).Duration() > tolerancia)
            return ResultadoVerificacion.FueraDeVentana; // evita reenvíos de un cuerpo viejo
        var esperada = "sha256=" + Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes($"{timestamp}.{cuerpo}")));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(esperada), Encoding.UTF8.GetBytes(firma))
            ? ResultadoVerificacion.Valida
            : ResultadoVerificacion.FirmaIncorrecta;
    }
}

public enum ResultadoVerificacion { Valida, Faltan, TimestampInvalido, FueraDeVentana, FirmaIncorrecta }
