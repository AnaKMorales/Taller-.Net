using System.Net;
using System.Net.Sockets;
using System.Text;
using UltimaMilla.Application.Avisos;
using UltimaMilla.Infrastructure.Avisos;

namespace UltimaMilla.Infrastructure.Tests;

public sealed class DestinoSeguroTests
{
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("93.184.216.34")]
    [InlineData("172.15.0.1")]     // justo fuera de 172.16/12
    [InlineData("172.32.0.1")]
    [InlineData("100.63.0.1")]     // justo fuera del CGNAT
    [InlineData("2606:4700:4700::1111")]
    public void AceptaDireccionesPublicas(string ip) => Assert.True(DestinoSeguro.EsPublica(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.1.2.3")]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")]  // metadatos de la nube
    [InlineData("100.64.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:127.0.0.1")]   // IPv4 disfrazada de IPv6
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    public void RechazaDireccionesInternas(string ip) => Assert.False(DestinoSeguro.EsPublica(IPAddress.Parse(ip)));
}

public sealed class ClienteWebhookHttpTests : IDisposable
{
    private readonly HttpListener _escucha = new();
    private readonly string _base;
    private readonly List<(string Ruta, string Cuerpo, System.Collections.Specialized.NameValueCollection Encabezados)> _recibidos = [];
    private Func<HttpListenerContext, Task> _responder = ctx => { ctx.Response.StatusCode = 200; return Task.CompletedTask; };
    private readonly Task _bucle;
    private readonly CancellationTokenSource _fin = new();

    public ClienteWebhookHttpTests()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0); tcp.Start();
        var puerto = ((IPEndPoint)tcp.LocalEndpoint).Port; tcp.Stop();
        _base = $"http://127.0.0.1:{puerto}/";
        _escucha.Prefixes.Add(_base);
        _escucha.Start();
        _bucle = Task.Run(async () =>
        {
            while (!_fin.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _escucha.GetContextAsync(); } catch { return; }
                _ = Task.Run(async () =>
                {
                    using var lector = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    var cuerpo = await lector.ReadToEndAsync();
                    lock (_recibidos) _recibidos.Add((ctx.Request.Url!.AbsolutePath, cuerpo, ctx.Request.Headers));
                    try { await _responder(ctx); ctx.Response.Close(); } catch { }
                });
            }
        });
    }

    public void Dispose() { _fin.Cancel(); _escucha.Close(); }

    private static ClienteWebhookHttp Cliente(bool privados = true, double segundos = 5) =>
        new(new OpcionesWebhook { PermitirDestinosPrivados = privados, Timeout = TimeSpan.FromSeconds(segundos) });

    private SolicitudWebhook Solicitud(string ruta = "hook", string cuerpo = """{"x":"ñ"}""") =>
        new(new Uri(_base + ruta), "secreto", cuerpo, Guid.Parse("11111111-2222-3333-4444-555555555555"), DateTimeOffset.FromUnixTimeSeconds(1760000000));

    [Fact]
    public async Task EnviaCuerpoYLosTresEncabezadosConUnaFirmaQueElReceptorPuedeVerificar()
    {
        using var cliente = Cliente();
        var solicitud = Solicitud();

        var resultado = await cliente.EnviarAsync(solicitud);

        Assert.True(resultado.Exitoso);
        Assert.Equal(200, resultado.CodigoHttp);
        var (ruta, cuerpo, h) = Assert.Single(_recibidos);
        Assert.Equal("/hook", ruta);
        Assert.Equal(solicitud.Cuerpo, cuerpo);
        Assert.Equal("1760000000", h["X-Timestamp"]);
        Assert.Equal("11111111-2222-3333-4444-555555555555", h["X-Event-Id"]);
        Assert.StartsWith("application/json", h["Content-Type"]);
        Assert.Equal(FirmaWebhook.Calcular("secreto", 1760000000, cuerpo), h["X-Signature"]);
        Assert.True(FirmaWebhook.Verificar("secreto", h["X-Timestamp"], cuerpo, h["X-Signature"],
            DateTimeOffset.FromUnixTimeSeconds(1760000000), TimeSpan.FromMinutes(5)));
        Assert.False(FirmaWebhook.Verificar("otro", h["X-Timestamp"], cuerpo, h["X-Signature"],
            DateTimeOffset.FromUnixTimeSeconds(1760000000), TimeSpan.FromMinutes(5)));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(404)]
    [InlineData(429)]
    public async Task UnaRespuestaNo2xxEsUnFalloYNoSeReintentaEnElMismoProceso(int estado)
    {
        _responder = ctx => { ctx.Response.StatusCode = estado; return Task.CompletedTask; };
        using var cliente = Cliente();

        var resultado = await cliente.EnviarAsync(Solicitud());

        Assert.False(resultado.Exitoso);
        Assert.Equal(estado, resultado.CodigoHttp);
        Assert.Single(_recibidos); // un solo intento
    }

    [Fact]
    public async Task UnServidorLentoFallaPorTimeoutCortoSinReintentar()
    {
        using var cliente = Cliente(segundos: 1);
        // Calentamiento: el JIT, el DNS y la conexión (que queda en el pool) se pagan acá, fuera de la ventana del timeout,
        // para que en una máquina cargada el pedido medido llegue al servidor mucho antes de que venza.
        Assert.True((await cliente.EnviarAsync(Solicitud("calentar"))).Exitoso);
        // El servidor recibe el pedido y no responde nunca: la única forma de terminar es el timeout del cliente,
        // sin depender de cuánto tarda el servidor ni de un cronómetro.
        _responder = ctx => Task.Delay(Timeout.Infinite, _fin.Token);

        // WaitAsync solo acota un cuelgue (si el timeout no funcionara); no mide tiempos finos.
        var resultado = await cliente.EnviarAsync(Solicitud()).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(resultado.Exitoso);
        Assert.Equal("Tiempo de espera agotado", resultado.Motivo);
        // El servidor registra el pedido en otra tarea: se espera la condición (no un tiempo fijo) antes de contar.
        await EsperarAsync(() => Recibidos("/hook") >= 1);
        await Task.Delay(TimeSpan.FromMilliseconds(500)); // un reintento llegaría dentro de este margen
        Assert.Equal(1, Recibidos("/hook")); // un solo intento
    }

    private int Recibidos(string ruta) { lock (_recibidos) return _recibidos.Count(r => r.Ruta == ruta); }

    private static async Task EsperarAsync(Func<bool> condicion, int maximoMs = 10_000)
    {
        var limite = Environment.TickCount64 + maximoMs;
        while (!condicion())
        {
            if (Environment.TickCount64 > limite) throw new TimeoutException("La condición esperada no ocurrió.");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task NoSigueRedireccionesAOtraDireccion()
    {
        _responder = ctx => { ctx.Response.StatusCode = 302; ctx.Response.RedirectLocation = _base + "interno"; return Task.CompletedTask; };
        using var cliente = Cliente();

        var resultado = await cliente.EnviarAsync(Solicitud());

        Assert.False(resultado.Exitoso);
        Assert.Equal(302, resultado.CodigoHttp);
        Assert.DoesNotContain(_recibidos, r => r.Ruta == "/interno");
    }

    [Fact]
    public async Task UnDestinoInternoSeRechazaSinConectarPorDefecto()
    {
        using var cliente = Cliente(privados: false); // el listener está en 127.0.0.1
        var resultado = await cliente.EnviarAsync(Solicitud());
        Assert.False(resultado.Exitoso);
        Assert.Equal("Destino no permitido", resultado.Motivo);
        Assert.Empty(_recibidos);
    }

    [Fact]
    public async Task UnDestinoQueNoResponde_EsFalloDeConexionNoExcepcion()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0); tcp.Start();
        var puerto = ((IPEndPoint)tcp.LocalEndpoint).Port; tcp.Stop(); // puerto cerrado
        using var cliente = Cliente();
        var resultado = await cliente.EnviarAsync(new SolicitudWebhook(new Uri($"http://127.0.0.1:{puerto}/"), "s", "{}", Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.False(resultado.Exitoso);
        Assert.Equal("Error de conexión", resultado.Motivo);
    }

    [Fact]
    public async Task LaCancelacionPorApagadoSiLanzaExcepcion()
    {
        _responder = async ctx => await Task.Delay(TimeSpan.FromSeconds(3));
        using var cliente = Cliente(segundos: 10);
        using var cancelar = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cliente.EnviarAsync(Solicitud(), cancelar.Token));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(31000)]
    public void ElTimeoutTieneLimites(int ms) =>
        Assert.Throws<InvalidOperationException>(() => new OpcionesWebhook { Timeout = TimeSpan.FromMilliseconds(ms) }.Validar());
}
