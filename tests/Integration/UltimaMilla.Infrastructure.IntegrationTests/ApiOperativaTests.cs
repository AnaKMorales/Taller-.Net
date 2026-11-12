using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UltimaMilla.Application.Configuracion;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Web.Api;
using UltimaMilla.Web.Components;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private static readonly JsonSerializerOptions JsonApi = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };

    // Solo el servidor de pruebas acepta estos headers. Producción conserva su autenticación.
    private sealed class IdentidadPrueba(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Usuario", out var usuario))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, usuario.ToString()),
                new Claim("operador_id", Request.Headers["X-Test-Operador"].ToString()),
                new Claim(ClaimTypes.Role, Request.Headers["X-Test-Rol"].ToString()) };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class ServidorApi(string connection, bool usarIdentidadPrueba = true, string ambiente = "Testing",
        bool exportarTelemetria = false)
        : WebApplicationFactory<App>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(ambiente);
            builder.UseSetting("OpenTelemetry:ExportarOtlp", exportarTelemetria.ToString());
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:UltimaMilla"] = connection, ["ConnectionStrings:Redis"] = "",
                ["Repartidor:Origenes:0"] = "https://repartidor.example",
                ["Logging:LogLevel:Default"] = "Warning",
                ["OpenTelemetry:ExportarOtlp"] = exportarTelemetria.ToString()
            }));
            builder.ConfigureServices(services =>
            {
                if (!usarIdentidadPrueba) return;
                services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(new RelojConfiguracion());
                services.AddAuthentication(o => o.DefaultAuthenticateScheme = "Test")
                    .AddScheme<AuthenticationSchemeOptions, IdentidadPrueba>("Test", _ => { });
            });
        }
        internal HttpClient Cliente(EscenarioPersistencia? e = null, Usuario? usuario = null)
        {
            var client = CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
            if (e is not null)
            {
                usuario ??= e.Repartidor;
                client.DefaultRequestHeaders.Add("X-Test-Usuario", usuario.Id.ToString());
                client.DefaultRequestHeaders.Add("X-Test-Operador", e.Operador.Id.ToString());
                client.DefaultRequestHeaders.Add("X-Test-Rol", usuario.Rol.ToString());
            }
            return client;
        }
    }

    private static async Task Antiforgery(HttpClient client)
    {
        var tokens = await client.GetFromJsonAsync<JsonElement>("/api/antiforgery");
        client.DefaultRequestHeaders.Remove(ApiOperativa.CabeceraAntiforgery);
        client.DefaultRequestHeaders.Add(ApiOperativa.CabeceraAntiforgery, tokens.GetProperty("token").GetString());
    }

    [PostgresFact]
    public async Task ApiHojaDeRutaExigeIdentidadYNoMuestraRutasSinDespachar()
    {
        var e = await GuardarEscenario(recibido: true);
        Guid rutaId;
        await using (var db = Context())
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            var ruta = await NuevaRuta(db);
            ruta.AgregarParada(envio, 1, EscenarioPersistencia.Fecha.AddHours(1), envio.Eventos.First().Responsable!, EscenarioPersistencia.Fecha);
            db.Add(ruta); await db.SaveChangesAsync(); rutaId = ruta.Id;
        }
        await using var server = new ServidorApi(_testConnection);
        using var anonimo = server.Cliente();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/repartidor/rutas")).StatusCode);
        using var admin = server.Cliente(e, e.Admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/repartidor/rutas")).StatusCode);
        using var repartidor = server.Cliente(e);
        Assert.Empty((await repartidor.GetFromJsonAsync<RutaDisponibleDto[]>("/api/repartidor/rutas", JsonApi))!);
        Assert.Equal(HttpStatusCode.NotFound, (await repartidor.GetAsync($"/api/repartidor/rutas/{rutaId}")).StatusCode);
        await using (var db = Context(e.Operador.Id))
        {
            await DespacharVisita(db, rutaId, e.Admin.Id); await db.SaveChangesAsync();
        }
        Assert.Single((await repartidor.GetFromJsonAsync<RutaDisponibleDto[]>("/api/repartidor/rutas", JsonApi))!);
    }

    [PostgresFact]
    public async Task ApiHojaDeRutaDevuelveOrdenVersionesYBultosSinModificarLaBase()
    {
        var (e, _, _, _) = await DosCuentas();
        Guid rutaId;
        await using (var db = Context(e.Operador.Id))
        {
            var envios = await EscenarioPersistencia.Grafo(db).OrderBy(e => e.Id).ToListAsync();
            var admin = await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id);
            var ruta = await NuevaRuta(db);
            foreach (var envio in envios)
            {
                foreach (var bulto in envio.Bultos) EscaneoBulto.RegistrarRecepcion(bulto, admin, EscenarioPersistencia.Fecha);
                envio.RecibirEnDeposito(admin, EscenarioPersistencia.Fecha);
            }
            ruta.AgregarParada(envios[0], 20, EscenarioPersistencia.Fecha.AddHours(1), admin, EscenarioPersistencia.Fecha);
            ruta.AgregarParada(envios[1], 10, EscenarioPersistencia.Fecha.AddHours(1), admin, EscenarioPersistencia.Fecha);
            db.Add(ruta); await db.SaveChangesAsync();
            ruta.Despachar(admin, EscenarioPersistencia.Fecha); await db.SaveChangesAsync(); rutaId = ruta.Id;
        }
        await using var server = new ServidorApi(_testConnection);
        using var client = server.Cliente(e);
        var response = await client.GetAsync($"/api/repartidor/rutas/{rutaId}"); response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl!.NoStore);
        var hoja = (await response.Content.ReadFromJsonAsync<HojaRutaDto>(JsonApi))!;
        Assert.Equal(new[] { 10, 20 }, hoja.Paradas.Select(p => p.Orden));
        Assert.Equal(e.Vehiculo.Matricula, hoja.Vehiculo.Matricula);
        await using var reader = Context(e.Operador.Id);
        Assert.Equal(hoja.Version, await reader.Set<Ruta>().Select(r => EF.Property<Guid>(r, "RowVersion")).SingleAsync());
        foreach (var parada in hoja.Paradas)
        {
            Assert.Single(parada.Bultos); Assert.NotEmpty(parada.Destinatario.Direccion.Calle);
            Assert.Equal(e.Reglas.Id, parada.Reglas.VersionReglasId);
            Assert.Equal(TipoPrueba.Foto, Assert.Single(parada.Reglas.PruebasExigidas));
            Assert.Single(parada.Reglas.Motivos);
            Assert.Equal(parada.VersionEsperadaEnvio, await reader.Envios.Where(x => x.Id == parada.EnvioId)
                .Select(x => EF.Property<Guid>(x, "RowVersion")).SingleAsync());
        }
        Assert.Equal(8, await reader.Set<UltimaMilla.Infrastructure.Persistence.Outbox.MensajeOutbox>().CountAsync());
        Assert.Empty(await reader.Set<IntentoEntrega>().ToListAsync());
    }

    [PostgresFact]
    public async Task ApiHojaDeRutaAislaOperadorRepartidorYRechazaUsuarioRevocado()
    {
        var e = await PrepararVisitaParcial();
        Guid rutaId;
        Repartidor noAsignado;
        await using (var db = Context(e.Operador.Id))
        {
            rutaId = await db.Set<Ruta>().Select(r => r.Id).SingleAsync();
            noAsignado = new Repartidor(await db.Set<Operador>().SingleAsync(), "otro-repartidor@example.com");
            db.Add(noAsignado); await db.SaveChangesAsync();
        }
        var otro = await GuardarEscenario();
        await using var server = new ServidorApi(_testConnection);
        using var ajeno = server.Cliente(otro);
        using var noAsignadoClient = server.Cliente(e, noAsignado);
        foreach (var client in new[] { ajeno, noAsignadoClient })
        {
            Assert.Empty((await client.GetFromJsonAsync<RutaDisponibleDto[]>("/api/repartidor/rutas", JsonApi))!);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/repartidor/rutas/{rutaId}")).StatusCode);
        }
        await using (var db = Context(e.Operador.Id))
        {
            var user = await db.Set<Repartidor>().SingleAsync(u => u.Id == e.Repartidor.Id);
            db.Entry(user).Property(u => u.Activo).CurrentValue = false; await db.SaveChangesAsync();
        }
        using var revocado = server.Cliente(e);
        Assert.Equal(HttpStatusCode.Unauthorized, (await revocado.GetAsync($"/api/repartidor/rutas/{rutaId}")).StatusCode);
    }

    [PostgresFact]
    public async Task ApiHojaDeRutaIncluyeSoloLaComposicionDeLaVisitaPendienteYExcluyeRendidas()
    {
        var (e, rutaId) = await PrepararVisitaConRecargo();
        await using (var db = Context(e.Operador.Id))
        { await DespacharVisita(db, rutaId, e.Admin.Id); await db.SaveChangesAsync(); }
        await using var server = new ServidorApi(_testConnection);
        using var client = server.Cliente(e);
        var hoja = (await client.GetFromJsonAsync<HojaRutaDto>($"/api/repartidor/rutas/{rutaId}", JsonApi))!;
        var parada = Assert.Single(hoja.Paradas);
        Assert.Equal("B003", Assert.Single(parada.Bultos).Codigo);
        Assert.Equal(1, parada.Bultos.Single().PesoKg);
        Assert.Equal(25.50m, parada.Reglas.ImporteRecargoNuevaVisita);
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, parada.EstadoEnvio);
        Guid primera;
        await using (var db = Context(e.Operador.Id))
        {
            primera = await db.Set<Ruta>().Where(r => r.Id != rutaId).Select(r => r.Id).SingleAsync();
            (await CargarRuta(db, primera)).Rendir(EscenarioPersistencia.Fecha.AddHours(2));
            await db.SaveChangesAsync();
        }
        Assert.Single((await client.GetFromJsonAsync<RutaDisponibleDto[]>("/api/repartidor/rutas", JsonApi))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/repartidor/rutas/{primera}")).StatusCode);
    }

    [PostgresFact]
    public async Task ApiConfiguracionVersionaEnCalienteConPermisosAntiforgeryYConservaElEnvioAnterior()
    {
        var e = await GuardarEscenario();
        var otro = await GuardarEscenario();
        await using var server = new ServidorApi(_testConnection);
        using var admin = server.Cliente(e, e.Admin);
        using var repartidor = server.Cliente(e);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync("/api/configuracion/reglas")).StatusCode);
        var solicitud = new NuevaVersionReglas(1, null, DatosReglas());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/configuracion/reglas", solicitud, JsonApi)).StatusCode);
        await Antiforgery(admin);
        var creadaResponse = await admin.PostAsJsonAsync("/api/configuracion/reglas", solicitud, JsonApi);
        Assert.Equal(HttpStatusCode.Created, creadaResponse.StatusCode);
        var creada = (await creadaResponse.Content.ReadFromJsonAsync<VersionCreadaDto>(JsonApi))!;
        Assert.Equal(2, creada.Version.Version);
        var detalle = (await admin.GetFromJsonAsync<DetalleReglasDto>(creadaResponse.Headers.Location!, JsonApi))!;
        Assert.Equal(4, detalle.Datos.MaxIntentosEntrega);
        Assert.Equal(creada.Version.Id, (await admin.GetFromJsonAsync<DetalleReglasDto>("/api/configuracion/reglas/vigentes", JsonApi))!.Version.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/configuracion/reglas", solicitud, JsonApi)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/configuracion/reglas",
            solicitud with { UltimaVersionEsperada = 2, Datos = DatosReglas() with { MaxIntentosEntrega = 0 } }, JsonApi)).StatusCode);
        using var ajeno = server.Cliente(otro, otro.Admin);
        Assert.Equal(HttpStatusCode.NotFound, (await ajeno.GetAsync(creadaResponse.Headers.Location)).StatusCode);
        await using var reader = Context(e.Operador.Id);
        var envio = await reader.Envios.Include(x => x.Reglas).SingleAsync();
        Assert.Equal(e.Reglas.Id, envio.Reglas.Id); Assert.Equal(2, envio.Reglas.MaxIntentosEntrega);
        Assert.Equal(2, await reader.Set<ReglasOperativas>().CountAsync());
    }

    [PostgresFact]
    public async Task ApiTarifarioActualizaCacheSinCambiarLaTarifaHistoricaNiAceptarZonasAjenas()
    {
        var e = await GuardarEscenario(); var otro = await GuardarEscenario();
        await using var server = new ServidorApi(_testConnection);
        using var admin = server.Cliente(e, e.Admin);
        var viejo = (await admin.GetFromJsonAsync<DetalleTarifarioDto>("/api/configuracion/tarifarios/vigente", JsonApi))!;
        using var scope = server.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ITarifarioVigente>();
        Assert.Equal(viejo.Version.Id, (await cache.ObtenerAsync(e.Operador.Id))!.CuadroId);
        await Antiforgery(admin);
        var response = await admin.PostAsJsonAsync("/api/configuracion/tarifarios", new NuevaVersionTarifario(1, null, [NuevaLinea(e.Zona.Id)]), JsonApi);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var nueva = (await response.Content.ReadFromJsonAsync<VersionCreadaDto>(JsonApi))!;
        Assert.False(nueva.RevisarInvalidacionCache);
        var vigente = (await admin.GetFromJsonAsync<DetalleTarifarioDto>("/api/configuracion/tarifarios/vigente", JsonApi))!;
        Assert.Equal(nueva.Version.Id, vigente.Version.Id); Assert.NotEqual(viejo.Version.Id, vigente.Version.Id);
        Assert.Equal(200, vigente.Lineas.Single().Precio);
        Assert.Equal(nueva.Version.Id, (await cache.ObtenerAsync(e.Operador.Id))!.CuadroId);
        Assert.Equal(otro.Envio.CuadroTarifarioId, (await cache.ObtenerAsync(otro.Operador.Id))!.CuadroId);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/configuracion/tarifarios",
            new NuevaVersionTarifario(2, null, [NuevaLinea(otro.Zona.Id)]), JsonApi)).StatusCode);
        await using var reader = Context(e.Operador.Id);
        var envio = await reader.Envios.Include(e => e.CuadroTarifario).SingleAsync();
        Assert.Equal(e.Envio.TarifaCalculada, envio.TarifaCalculada);
        Assert.Equal(e.Envio.CuadroTarifarioId, envio.CuadroTarifarioId);
        Assert.Equal(2, await reader.Set<CuadroTarifario>().CountAsync());
    }
}
