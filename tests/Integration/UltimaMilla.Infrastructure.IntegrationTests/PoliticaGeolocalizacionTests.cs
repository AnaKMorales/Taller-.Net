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

    // La cámara solo para la PWA (escaneo de bultos con BarcodeDetector); el micrófono, nunca.
    [Theory]
    [InlineData("/repartidor/", "camera=(self)")]
    [InlineData("/login", "camera=()")]
    [InlineData("/seguimiento/abc", "camera=()")]
    public async Task SoloLaPwaDelRepartidorPuedeUsarLaCamara(string ruta, string esperado)
    {
        using var host = new WebApplicationFactory<App>();
        using var server = host.WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        using var client = server.CreateClient();

        using var respuesta = await client.GetAsync(ruta);

        var politica = Assert.Single(respuesta.Headers.GetValues("Permissions-Policy"));
        Assert.Contains(esperado, politica);
        Assert.Contains("microphone=()", politica);
    }
}
