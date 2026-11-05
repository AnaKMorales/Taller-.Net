using System.Net;
using System.Net.Sockets;
using System.Text;
using UltimaMilla.Application.Avisos;

namespace UltimaMilla.Infrastructure.Avisos;

// Un único intento por llamada: timeout corto, sin redirecciones, sin proxy y sin reintentos internos.
public sealed class ClienteWebhookHttp : IClienteWebhook, IDisposable
{
    private readonly HttpClient _http;

    public ClienteWebhookHttp(OpcionesWebhook opciones)
    {
        opciones.Validar();
        var manejador = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,   // una redirección podría llevar a una dirección interna
            UseProxy = false,
            UseCookies = false,
            ConnectTimeout = opciones.Timeout,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = (contexto, token) => ConectarAsync(contexto, opciones.PermitirDestinosPrivados, token),
        };
        _http = new HttpClient(manejador, disposeHandler: true) { Timeout = opciones.Timeout };
    }

    public async Task<ResultadoWebhook> EnviarAsync(SolicitudWebhook solicitud, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var timestamp = solicitud.Momento.ToUnixTimeSeconds();
        using var pedido = new HttpRequestMessage(HttpMethod.Post, solicitud.Destino)
        {
            Content = new StringContent(solicitud.Cuerpo, new UTF8Encoding(false), "application/json"),
        };
        pedido.Headers.TryAddWithoutValidation(FirmaWebhook.EncabezadoFirma, FirmaWebhook.Calcular(solicitud.SecretoFirma, timestamp, solicitud.Cuerpo));
        pedido.Headers.TryAddWithoutValidation(FirmaWebhook.EncabezadoTimestamp, timestamp.ToString());
        pedido.Headers.TryAddWithoutValidation(FirmaWebhook.EncabezadoEvento, solicitud.EventoId.ToString());
        try
        {
            using var respuesta = await _http.SendAsync(pedido, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var codigo = (int)respuesta.StatusCode;
            return respuesta.IsSuccessStatusCode ? ResultadoWebhook.Ok(codigo) : ResultadoWebhook.Fallo($"HTTP {codigo}", codigo);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return ResultadoWebhook.Fallo("Tiempo de espera agotado"); }
        catch (HttpRequestException error) { return ResultadoWebhook.Fallo(error.InnerException is DestinoNoPermitidoException ? "Destino no permitido" : "Error de conexión"); }
    }

    private static async ValueTask<Stream> ConectarAsync(SocketsHttpConnectionContext contexto, bool permitirPrivados, CancellationToken token)
    {
        var host = contexto.DnsEndPoint.Host;
        // Se resuelve aquí y se conecta a la IP ya validada: evita que el DNS cambie entre la validación y la conexión.
        var direcciones = await Dns.GetHostAddressesAsync(host, token);
        var candidatas = permitirPrivados ? direcciones : direcciones.Where(DestinoSeguro.EsPublica).ToArray();
        if (candidatas.Length == 0) throw new DestinoNoPermitidoException();

        Exception? ultimo = null;
        foreach (var direccion in candidatas)
        {
            var socket = new Socket(direccion.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(direccion, contexto.DnsEndPoint.Port), token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception error) when (error is SocketException)
            {
                socket.Dispose();
                ultimo = error;
            }
            catch { socket.Dispose(); throw; }
        }
        throw new HttpRequestException("No se pudo conectar.", ultimo);
    }

    public void Dispose() => _http.Dispose();

    private sealed class DestinoNoPermitidoException : Exception
    {
        public DestinoNoPermitidoException() : base("El destino no es una dirección pública.") { }
    }
}
