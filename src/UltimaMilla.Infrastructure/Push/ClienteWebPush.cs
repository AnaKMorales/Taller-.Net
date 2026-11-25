using System.Net;
using UltimaMilla.Infrastructure.Avisos;

namespace UltimaMilla.Infrastructure.Push;

public enum ResultadoPush
{
    Entregado,   // el servicio push lo aceptó (201)
    Vencida,     // 404/410: el dispositivo se dio de baja; la suscripción se borra
    Rechazado,   // 400/403/413...: reintentar no lo arregla; se registra y se sigue
}

public interface IClienteWebPush
{
    // Lanza HttpRequestException ante fallos transitorios (sin conexión, timeout, 429, 5xx): el mensaje se reintenta.
    Task<ResultadoPush> EnviarAsync(Uri endpoint, string p256dh, string auth, byte[] contenido, string etiqueta,
        CancellationToken cancellationToken = default);
}

// Un intento por llamada, como los webhooks: sin redirecciones, sin proxy, timeout corto y solo IPs públicas
// de los servicios push permitidos.
public sealed class ClienteWebPush : IClienteWebPush, IDisposable
{
    private readonly HttpClient _http;
    private readonly OpcionesWebPush _opciones;
    private readonly TimeProvider _reloj;

    public ClienteWebPush(OpcionesWebPush opciones, TimeProvider reloj)
    {
        _opciones = opciones;
        _reloj = reloj;
        var manejador = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = opciones.Timeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = (contexto, token) => ClienteWebhookHttp.ConectarAsync(contexto, permitirPrivados: false, token),
        };
        _http = new HttpClient(manejador, disposeHandler: true) { Timeout = opciones.Timeout };
    }

    public async Task<ResultadoPush> EnviarAsync(Uri endpoint, string p256dh, string auth, byte[] contenido, string etiqueta,
        CancellationToken cancellationToken = default)
    {
        if (!_opciones.EsServicioPermitido(endpoint)) return ResultadoPush.Rechazado;
        using var pedido = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(CifradoWebPush.Cifrar(contenido, p256dh, auth)),
        };
        pedido.Content.Headers.TryAddWithoutValidation("Content-Type", "application/octet-stream");
        pedido.Content.Headers.TryAddWithoutValidation("Content-Encoding", "aes128gcm");
        pedido.Headers.TryAddWithoutValidation("Authorization", FirmaVapid.Encabezado(endpoint, _opciones, _reloj.GetUtcNow()));
        pedido.Headers.TryAddWithoutValidation("TTL", ((int)_opciones.TiempoDeVida.TotalSeconds).ToString());
        pedido.Headers.TryAddWithoutValidation("Urgency", "high");
        // Topic (máx. 32 caracteres base64url): si el celular estaba apagado, el servicio guarda solo el último aviso
        // de este envío en lugar de acumularlos.
        pedido.Headers.TryAddWithoutValidation("Topic", etiqueta.Length > 32 ? etiqueta[^32..] : etiqueta);
        try
        {
            using var respuesta = await _http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return respuesta.StatusCode switch
            {
                _ when respuesta.IsSuccessStatusCode => ResultadoPush.Entregado,
                HttpStatusCode.NotFound or HttpStatusCode.Gone => ResultadoPush.Vencida,
                HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError =>
                    throw new HttpRequestException($"El servicio push respondió {(int)respuesta.StatusCode}.", null, respuesta.StatusCode),
                _ => ResultadoPush.Rechazado,
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException("Tiempo de espera agotado con el servicio push.");
        }
    }

    public void Dispose() => _http.Dispose();
}
