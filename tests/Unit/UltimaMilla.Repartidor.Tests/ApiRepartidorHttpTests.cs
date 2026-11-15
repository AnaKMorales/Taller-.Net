using System.Net;
using System.Text;
using Microsoft.JSInterop;
using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Dispositivo;
using UltimaMilla.Repartidor.Servidor;
using UltimaMilla.Repartidor.Simulacion;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Tests;

// Cliente HTTP contra respuestas del servidor, sin red ni navegador.
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
    public async Task DescargaLaRutaElegidaYPermiteRecuperarlaDelAlmacenSinRed()
    {
        var ruta = Guid.NewGuid();
        var otraRuta = Guid.NewGuid();
        var parada = Guid.NewGuid();
        var envio = Guid.NewGuid();
        var version = Guid.NewGuid();
        var versionEnvio = Guid.NewGuid();
        var bulto = Guid.NewGuid();
        _servidor.Responder(HttpMethod.Get, HttpStatusCode.OK, $$"""
            [{"rutaId":"{{otraRuta}}","despachadaEn":"2026-11-05T12:00:00Z","matricula":"OTRA","paradasPendientes":1},
             {"rutaId":"{{ruta}}","despachadaEn":"2026-11-05T11:00:00Z","matricula":"ABC","paradasPendientes":1}]
            """);
        _servidor.Responder(HttpMethod.Get, HttpStatusCode.OK, $$"""
            {"rutaId":"{{ruta}}","version":"{{version}}","paradas":[{
                "paradaId":"{{parada}}","orden":4,"envioId":"{{envio}}","versionEsperadaEnvio":"{{versionEnvio}}",
                "estadoEnvio":"EntregadoParcialmente","ventanaDesde":"2026-11-05T12:00:00Z","ventanaHasta":"2026-11-05T14:00:00Z",
                "destinatario":{"nombre":"Elena","direccion":{"calle":"Calle","numero":"12","localidad":"Montevideo","codigoPostal":"11000"} },
                "reglas":{"versionReglasId":"{{Guid.NewGuid()}}","pruebasExigidas":["Foto"],"motivos":[],"permiteEntregaParcial":true},
                "bultos":[{"bultoId":"{{bulto}}","codigo":"B-1","estado":"EnTransito","pesoKg":2,"volumenM3":0.5}]}]}
            """);
        var sesion = Datos.Sesion with { ExpiraEn = Datos.Inicio.AddHours(1) };
        var almacen = new AlmacenEnMemoria();
        var datos = new DatosLocales(almacen);
        await datos.GuardarSesionAsync(sesion);

        await datos.GuardarHojaAsync(sesion, await _api.ObtenerHojaDeRutaAsync(sesion, ruta));
        var guardada = await new DatosLocales(almacen).ObtenerHojaAsync();

        Assert.NotNull(guardada);
        Assert.Equal(ruta, guardada.RutaId);
        Assert.Equal(version, guardada.VersionRuta);
        Assert.Equal(Datos.Inicio, guardada.DescargadaEn);
        Assert.Equal(2, guardada.RutasDisponibles!.Count);
        var detalle = Assert.Single(guardada.Paradas);
        Assert.Equal(parada, detalle.ParadaId);
        Assert.Equal(versionEnvio, detalle.VersionEsperadaEnvio);
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, detalle.Estado);
        Assert.Equal("Calle 12, Montevideo (11000)", detalle.Direccion);
        Assert.Equal(bulto, Assert.Single(detalle.Bultos!).BultoId);
        Assert.Equal(TipoPrueba.Foto, Assert.Single(detalle.Reglas!.PruebasExigidas));
        Assert.Equal(new[] { "http://api.test/api/repartidor/rutas", $"http://api.test/api/repartidor/rutas/{ruta}" },
            _servidor.Pedidos.Select(p => p.Url));
        Assert.All(_servidor.Pedidos, pedido => Assert.Equal("Bearer t", pedido.Autorizacion));

        await datos.GuardarSesionAsync(sesion with { RepartidorId = Guid.NewGuid(), Token = "otro" });
        Assert.Null(await datos.ObtenerHojaAsync());
    }

    [Fact]
    public async Task SinRutasDisponiblesDevuelveUnaHojaVaciaSinConsultarDetalle()
    {
        _servidor.Responder(HttpMethod.Get, HttpStatusCode.OK, "[]");
        var hoja = await _api.ObtenerHojaDeRutaAsync(Datos.Sesion);

        Assert.Equal(Guid.Empty, hoja.RutaId);
        Assert.Empty(hoja.Paradas);
        Assert.Single(_servidor.Pedidos);
    }

    [Fact]
    public async Task SiLaRutaSeRindeDuranteLaDescargaPruebaLaSiguiente()
    {
        var rendida = Guid.NewGuid();
        var activa = Guid.NewGuid();
        _servidor.Responder(HttpMethod.Get, HttpStatusCode.OK, $$"""
            [{"rutaId":"{{rendida}}","despachadaEn":"2026-11-05T12:00:00Z","paradasPendientes":1},
             {"rutaId":"{{activa}}","despachadaEn":"2026-11-05T11:00:00Z","paradasPendientes":1}]
            """);
        _servidor.Responder(HttpMethod.Get, HttpStatusCode.NotFound, "");
        _servidor.Responder(HttpMethod.Get, HttpStatusCode.OK, $$"""{"rutaId":"{{activa}}","version":"{{Guid.NewGuid()}}","paradas":[]}""");

        var hoja = await _api.ObtenerHojaDeRutaAsync(Datos.Sesion);

        Assert.Equal(activa, hoja.RutaId);
        Assert.Equal(activa, Assert.Single(hoja.RutasDisponibles!).RutaId);
        Assert.Equal($"http://api.test/api/repartidor/rutas/{rendida}", _servidor.Pedidos[1].Url);
    }

    [Fact]
    public async Task UnEnvioRealNoSeConfirmaEnElSimulador()
    {
        var respuesta = await _api.EnviarAccionAsync(Datos.Sesion, Datos.Entrega(Guid.NewGuid()));
        Assert.Equal(ResultadoAccion.Rechazada, respuesta.Resultado);
        Assert.Empty(_servidor.Pedidos);
    }

    [Fact]
    public async Task VisitaParcialConservaElCuerpoUtcYReconoceLaConfirmacionRepetida()
    {
        var (_, accion) = Datos.VisitaParcial();
        _servidor.Responder(HttpMethod.Post, HttpStatusCode.ServiceUnavailable, "");
        _servidor.Responder(HttpMethod.Post, HttpStatusCode.OK, $$"""
            {"envioId":"{{accion.EnvioId}}","idOperacion":"{{accion.IdOperacion}}","repetido":true}
            """);

        await Assert.ThrowsAsync<HttpRequestException>(() => _api.EnviarAccionAsync(Datos.Sesion, accion));
        var respuesta = await _api.EnviarAccionAsync(Datos.Sesion, accion);

        Assert.Equal(ResultadoAccion.YaProcesada, respuesta.Resultado);
        Assert.Equal(_servidor.Pedidos[0], _servidor.Pedidos[1]);
        var pedido = _servidor.Pedidos[0];
        Assert.Equal("http://api.test/api/repartidor/intentos", pedido.Url);
        Assert.Equal("Bearer t", pedido.Autorizacion);
        using var json = System.Text.Json.JsonDocument.Parse(pedido.Cuerpo);
        var cuerpo = json.RootElement;
        Assert.Equal(accion.Intento!.VersionEsperada, cuerpo.GetProperty("versionEsperada").GetGuid());
        Assert.Equal("2026-11-05T12:00:00Z", cuerpo.GetProperty("ocurridoEn").GetString());
        Assert.Equal("Fallido", cuerpo.GetProperty("bultos")[1].GetProperty("resultado").GetString());
        Assert.Equal("Ana", cuerpo.GetProperty("evidencias")[0].GetProperty("texto").GetString());
    }

    [Fact]
    public async Task ConflictoExplicaQueDebeActualizarLaHoja()
    {
        var (_, accion) = Datos.VisitaParcial();
        _servidor.Responder(HttpMethod.Post, HttpStatusCode.Conflict, """{"detail":"El envío cambió."}""", "application/problem+json");

        var respuesta = await _api.EnviarAccionAsync(Datos.Sesion, accion);

        Assert.Equal(ResultadoAccion.Rechazada, respuesta.Resultado);
        Assert.Contains("El envío cambió. Actualizá la hoja", respuesta.Detalle);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConflictoPorBultoConservaDiagnosticoSoloDeLaParadaEnviada(bool mismaParada)
    {
        var (_, accion) = Datos.VisitaParcial();
        var intento = accion.Intento!;
        var parada = mismaParada ? intento.ParadaId : Guid.NewGuid();
        var version = Guid.NewGuid();
        _servidor.Responder(HttpMethod.Post, HttpStatusCode.Conflict, $$"""
            {"detail":"El envío cambió.","conflicto":{"paradaId":"{{parada}}","versionActualEnvio":"{{version}}","paradaActiva":false,
            "bultos":[{"bultoId":"{{intento.Bultos[0].BultoId}}","codigo":"B-01","estadoActual":"Entregado","motivo":"EstadoCambio"}] } }
            """, "application/problem+json");
        var respuesta = await _api.EnviarAccionAsync(Datos.Sesion, accion);
        Assert.Equal(ResultadoAccion.Rechazada, respuesta.Resultado);
        if (!mismaParada) { Assert.Null(respuesta.Conflicto); return; }
        Assert.Equal(version, respuesta.Conflicto!.VersionActualEnvio);
        Assert.Equal("B-01", Assert.Single(respuesta.Conflicto.Bultos).Codigo);
        Assert.Equal("EstadoCambio", respuesta.Conflicto.Bultos[0].Motivo);
    }

    private sealed record Pedido(string Url, string? Autorizacion, string Cuerpo);

    private sealed class ServidorHttpFalso : HttpMessageHandler
    {
        private readonly Dictionary<HttpMethod, Queue<(HttpStatusCode Codigo, string Json, string Tipo)>> _respuestas = [];
        public List<Pedido> Pedidos { get; } = [];

        public void Responder(HttpMethod metodo, HttpStatusCode codigo, string json, string tipo = "application/json")
        {
            if (!_respuestas.TryGetValue(metodo, out var respuestas)) _respuestas[metodo] = respuestas = new();
            respuestas.Enqueue((codigo, json, tipo));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var cuerpo = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Pedidos.Add(new Pedido(request.RequestUri!.ToString(), request.Headers.Authorization?.ToString(), cuerpo));
            var (codigo, json, tipo) = _respuestas[request.Method].Dequeue();
            return new HttpResponseMessage(codigo) { Content = new StringContent(json, Encoding.UTF8, tipo) };
        }
    }

    private sealed class JsSinNavegador : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => throw new NotSupportedException();
    }
}
