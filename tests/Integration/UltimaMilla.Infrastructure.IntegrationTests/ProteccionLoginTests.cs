using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Web.Components;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Infrastructure.IntegrationTests;

// Hallazgo H6: límite de tasa en los logins y bloqueo por (correo, IP) en lugar de solo por correo.
public sealed class ProteccionLoginTests
{
    private sealed class Reloj : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private static readonly IPAddress Atacante = IPAddress.Parse("203.0.113.7");
    private static readonly IPAddress Repartidor = IPAddress.Parse("198.51.100.20");

    [Fact]
    public void CincoFallosBloqueanSoloEseOrigenParaEseCorreo()
    {
        var reloj = new Reloj();
        var intentos = new IntentosLogin(reloj);

        for (var i = 0; i < IntentosLogin.MaximoFallos; i++) intentos.Fallo("repartidor@example.com", Atacante);

        Assert.True(intentos.Bloqueado(" REPARTIDOR@example.com ", Atacante)); // mismo correo normalizado
        Assert.False(intentos.Bloqueado("repartidor@example.com", Repartidor));  // el dueño sigue entrando
        Assert.False(intentos.Bloqueado("otro@example.com", Atacante));
        // Pasada la ventana, se libera.
        reloj.Ahora += IntentosLogin.Ventana;
        Assert.False(intentos.Bloqueado("repartidor@example.com", Atacante));
    }

    [Fact]
    public void UnLoginCorrectoReiniciaLosFallosYLasIpv6SeAgrupanPorPrefijo()
    {
        var intentos = new IntentosLogin(new Reloj());
        for (var i = 0; i < IntentosLogin.MaximoFallos - 1; i++) intentos.Fallo("a@example.com", Atacante);
        intentos.Exito("a@example.com", Atacante);
        intentos.Fallo("a@example.com", Atacante);
        Assert.False(intentos.Bloqueado("a@example.com", Atacante));

        // Rotar dentro del mismo /64 no evita el bloqueo; otro /64 es otro origen.
        for (var i = 1; i <= IntentosLogin.MaximoFallos; i++) intentos.Fallo("b@example.com", IPAddress.Parse($"2001:db8:1:2::{i}"));
        Assert.True(intentos.Bloqueado("b@example.com", IPAddress.Parse("2001:db8:1:2:ffff::1")));
        Assert.False(intentos.Bloqueado("b@example.com", IPAddress.Parse("2001:db8:1:3::1")));
        Assert.Equal("192.0.2.1", ProteccionLogin.Origen(IPAddress.Parse("::ffff:192.0.2.1")));
    }

    private sealed class LoginFalso : IBuscadorUsuariosLogin
    {
        public int Llamadas;
        public Task<UsuarioLogin?> ValidarAsync(string email, string contrasena, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Llamadas);
            return Task.FromResult<UsuarioLogin?>(null);
        }
    }

    private static WebApplicationFactory<App> Servidor(LoginFalso login, int porMinuto) =>
        new WebApplicationFactory<App>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
                { ["Login:IntentosPorMinuto"] = porMinuto.ToString() }));
            b.ConfigureServices(s => { s.RemoveAll<IBuscadorUsuariosLogin>(); s.AddSingleton<IBuscadorUsuariosLogin>(login); });
        });

    [Theory]
    [InlineData("/api/repartidor/sesion")]
    [InlineData("/api/comercios/sesion")]
    public async Task LasApisDeLoginCortanCon429AlSuperarElLimitePorIp(string ruta)
    {
        var login = new LoginFalso();
        using var servidor = Servidor(login, porMinuto: 3);
        using var cliente = servidor.CreateClient();
        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            using var r = await cliente.PostAsJsonAsync(ruta, new { email = $"u{i}@example.com", contrasena = "Clave-1!" });
            codigos.Add(r.StatusCode);
        }
        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized,
            HttpStatusCode.TooManyRequests], codigos);
        Assert.Equal(3, login.Llamadas);
    }

    [Fact]
    public async Task TrasCincoFallosElMismoOrigenNiSiquieraLlegaAValidarLaClave()
    {
        var login = new LoginFalso();
        using var servidor = Servidor(login, porMinuto: 100);
        using var cliente = servidor.CreateClient();
        for (var i = 0; i < IntentosLogin.MaximoFallos + 2; i++)
        {
            using var r = await cliente.PostAsJsonAsync("/api/repartidor/sesion", new { email = "rep@example.com", contrasena = "x" });
            // Misma respuesta antes y después del bloqueo: no revela que el origen quedó bloqueado.
            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        }
        Assert.Equal(IntentosLogin.MaximoFallos, login.Llamadas);
    }

    [Fact]
    public async Task ElFormularioDeLoginNoCuentaComoIntento()
    {
        using var servidor = Servidor(new LoginFalso(), porMinuto: 1);
        using var cliente = servidor.CreateClient();
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/login")).StatusCode);
    }
}
