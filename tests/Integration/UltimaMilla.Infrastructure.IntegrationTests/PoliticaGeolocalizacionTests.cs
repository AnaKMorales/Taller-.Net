using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using UltimaMilla.Web.Components;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed class PoliticaGeolocalizacionTests
{
    [Theory]
    [InlineData("/repartidor/", "geolocation=(self)")]
    [InlineData("/repartidor/parada/1", "geolocation=(self)")]
    [InlineData("/login", "geolocation=()")]
    public async Task SoloLaPwaDelRepartidorPuedeUsarLaUbicacion(string ruta, string esperado)
    {
        using var host = new WebApplicationFactory<App>();
        using var server = host.WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        using var client = server.CreateClient();

        using var respuesta = await client.GetAsync(ruta);

        Assert.Contains(esperado, Assert.Single(respuesta.Headers.GetValues("Permissions-Policy")));
    }

    // La cámara solo para la PWA y el depósito (escaneo de bultos); el micrófono, nunca.
    [Theory]
    [InlineData("/repartidor/", "camera=(self)")]
    [InlineData("/backoffice/deposito/rutas/00000000-0000-0000-0000-000000000001", "camera=(self)")]
    [InlineData("/login", "camera=()")]
    [InlineData("/backoffice/tablero", "camera=()")]
    [InlineData("/seguimiento/abc", "camera=()")]
    public async Task SoloLaPwaYElDepositoPuedenUsarLaCamara(string ruta, string esperado)
    {
        using var host = new WebApplicationFactory<App>();
        using var server = host.WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        // Sin sesión el backoffice redirige al login: se miran las cabeceras de la ruta pedida, no las del login
        using var client = server.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var respuesta = await client.GetAsync(ruta);

        var politica = Assert.Single(respuesta.Headers.GetValues("Permissions-Policy"));
        Assert.Contains(esperado, politica);
        Assert.Contains("microphone=()", politica);
    }
}
