using System.Security.Cryptography;
using System.Text;

namespace UltimaMilla.Application.Avisos;

// Firma de los webhooks. El comercio la verifica con el mismo secreto:
//   X-Timestamp: segundos Unix al momento del envío
//   X-Signature: "sha256=" + HMAC-SHA256(secreto, "{timestamp}.{cuerpo}") en hexadecimal minúscula
// El timestamp va firmado para que un atacante no pueda reenviar un cuerpo viejo con otra fecha.
public static class FirmaWebhook
{
    public const string EncabezadoFirma = "X-Signature";
    public const string EncabezadoTimestamp = "X-Timestamp";
    public const string EncabezadoEvento = "X-Event-Id";
    public const string Prefijo = "sha256=";

    public static string Calcular(string secreto, long timestamp, string cuerpo)
    {
        ArgumentException.ThrowIfNullOrEmpty(secreto);
        ArgumentNullException.ThrowIfNull(cuerpo);
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes($"{timestamp}.{cuerpo}"));
        return Prefijo + Convert.ToHexStringLower(hash);
    }

    // Para quien recibe el webhook (y para las pruebas). Comparación en tiempo constante y ventana contra reenvíos.
    public static bool Verificar(string secreto, string? timestamp, string cuerpo, string? firma,
        DateTimeOffset ahora, TimeSpan tolerancia)
    {
        if (!long.TryParse(timestamp, out var segundos) || string.IsNullOrEmpty(firma)) return false;
        if ((ahora - DateTimeOffset.FromUnixTimeSeconds(segundos)).Duration() > tolerancia) return false;
        var esperada = Encoding.UTF8.GetBytes(Calcular(secreto, segundos, cuerpo));
        return CryptographicOperations.FixedTimeEquals(esperada, Encoding.UTF8.GetBytes(firma));
    }
}

public sealed record SolicitudWebhook(Uri Destino, string SecretoFirma, string Cuerpo, Guid EventoId, DateTimeOffset Momento);

// Sin detalle del servidor del comercio: solo lo necesario para registrar el intento.
public sealed record ResultadoWebhook(bool Exitoso, int? CodigoHttp, string? Motivo)
{
    public static ResultadoWebhook Ok(int codigo) => new(true, codigo, null);
    public static ResultadoWebhook Fallo(string motivo, int? codigo = null) => new(false, codigo, motivo);
}

public interface IClienteWebhook
{
    // Un solo intento, con timeout corto. Un fallo de red, un timeout o un HTTP no 2xx devuelven Exitoso=false.
    // Solo la cancelación (apagado) lanza excepción.
    Task<ResultadoWebhook> EnviarAsync(SolicitudWebhook solicitud, CancellationToken cancellationToken = default);
}

// Al menos un aviso falló: el consumidor la clasifica como transitoria y el mensaje va a la cola de espera.
public sealed class AvisoWebhookFallidoException(Guid eventoId, IReadOnlyList<Guid> suscripciones)
    : Exception($"Falló el webhook del evento {eventoId} para {suscripciones.Count} suscripción(es).")
{
    public Guid EventoId { get; } = eventoId;
    public IReadOnlyList<Guid> Suscripciones { get; } = suscripciones;
}
