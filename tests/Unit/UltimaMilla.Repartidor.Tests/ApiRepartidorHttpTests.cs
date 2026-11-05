using System.Net;
using System.Text;
using Microsoft.JSInterop;
using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Dispositivo;
using UltimaMilla.Repartidor.Servidor;
using UltimaMilla.Repartidor.Simulacion;

namespace UltimaMilla.Repartidor.Tests;

// Cliente HTTP del login real, contra respuestas simuladas del servidor (sin red ni navegador).
public sealed class ApiRepartidorHttpTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private readonly ServidorHttpFalso _servidor = new();
    private readonly RelojManual _reloj = new(Datos.Inicio);
    private readonly ApiRepartidorHttp _api;

    public ApiRepartidorHttpTests()
    {
        // EstadoConexion arranca "en línea" sin tocar JavaScript; la simulación usa un almacén en memoria.
        var simulada = new ApiRepartidorFalsa(new AlmacenEnMemoria(), new EstadoConexion(new Navegador(new JsSinNavegador())));
        _api = new ApiRepartidorHttp(new HttpClient(_servidor) { BaseAddress = new Uri("http://api.test/") }, simulada, _reloj);
    }

    [Fact]
    public async Task ElLoginGuardaElTokenYTomaElPerfilDelServidor()
    {
        _servidor.Responder(HttpMethod.Post, HttpStatusCode.OK, """{"tokenType":"Bearer","accessToken":"tok-1","expiresIn":3600,"refreshToken":"r"}""");
        _servidor.Responder(HttpMethod.Get, HttpStatusCode.OK, $$"""{"usuarioId":"{{UsuarioId}}","email":"repartidor@demo.uy"}""");

        var sesion = await _api.IniciarSesionAsync("Repartidor@Demo.uy", "Demo1234!");

        Assert.NotNull(sesion);
        Assert.Equal("tok-1", sesion.Token);
        Assert.Equal(UsuarioId, sesion.RepartidorId);
        Assert.Equal("repartidor@demo.uy", sesion.Email);
        Assert.Equal(Datos.Inicio.AddHours(1), sesion.ExpiraEn);

        var login = _servidor.Pedidos[0];
        Assert.Equal("http://api.test/api/repartidor/sesion", login.Url);
        Assert.Contains("\"email\":\"Repartidor@Demo.uy\"", login.Cuerpo);
        Assert.DoesNotContain("operador", login.Cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Bearer tok-1", _servidor.Pedidos[1].Autorizacion);
    }

    [Fact]
    public async Task CredencialesInvalidasDevuelvenNull()
    {
        _servidor.Responder(HttpMethod.Post, HttpStatusCode.Unauthorized, "");
        Assert.Null(await _api.IniciarSesionAsync("x@demo.uy", "mala"));
    }

    [Fact]
    public async Task UnErrorDelServidorEnElLoginEsUnErrorDeConexion()
    {
        _servidor.Responder(HttpMethod.Post, HttpStatusCode.InternalServerError, "");
        await Assert.ThrowsAsync<HttpRequestException>(() => _api.IniciarSesionAsync("x@demo.uy", "Demo1234!"));
    }

    [Fact]
    public async Task UnTokenRechazadoPorElServidorEsSesionExpirada()
    {
        _servidor.Responder(HttpMethod.Get, HttpStatusCode.Unauthorized, "");
        var sesion = Datos.Sesion with { ExpiraEn = Datos.Inicio.AddHours(1) };

        await Assert.ThrowsAsync<SesionExpiradaException>(() => _api.ObtenerHojaDeRutaAsync(sesion));
        Assert.Equal("Bearer t", _servidor.Pedidos.Single().Autorizacion);
    }

    [Fact]
    public async Task UnTokenVencidoNoLlegaAlServidor()
    {
        var sesion = Datos.Sesion with { ExpiraEn = Datos.Inicio };

        await Assert.ThrowsAsync<SesionExpiradaException>(() => _api.EnviarAccionAsync(sesion, Datos.Entrega(Guid.NewGuid())));
        Assert.Empty(_servidor.Pedidos);
    }

    [Fact]
    public async Task ConTokenValidoSeVerificaYSeDescargaLaHoja()
    {
        _servidor.Responder(HttpMethod.Get, HttpStatusCode.OK, $$"""{"usuarioId":"{{UsuarioId}}","email":"repartidor@demo.uy"}""");
        var sesion = Datos.Sesion with { ExpiraEn = Datos.Inicio.AddHours(1) };

        var hoja = await _api.ObtenerHojaDeRutaAsync(sesion);

        Assert.NotEmpty(hoja.Paradas);
        Assert.Equal("Bearer t", _servidor.Pedidos.Single().Autorizacion);
    }

    private sealed record Pedido(string Url, string? Autorizacion, string Cuerpo);

    private sealed class ServidorHttpFalso : HttpMessageHandler
    {
        private readonly Dictionary<HttpMethod, (HttpStatusCode Codigo, string Json)> _respuestas = [];
        public List<Pedido> Pedidos { get; } = [];

        public void Responder(HttpMethod metodo, HttpStatusCode codigo, string json) => _respuestas[metodo] = (codigo, json);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var cuerpo = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Pedidos.Add(new Pedido(request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), cuerpo));
            var (codigo, json) = _respuestas[request.Method];
            return new HttpResponseMessage(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class JsSinNavegador : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => throw new NotSupportedException();
    }
}
