using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using UltimaMilla.Web.Components;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed class CorsHojaRutaTests
{
    [Theory]
    [InlineData("/api/repartidor/rutas")]
    [InlineData("/api/repartidor/rutas/00000000-0000-0000-0000-000000000001")]
    [InlineData("/api/repartidor/avisos/clave")]
    public async Task LaHojaDeRutaAdmiteElPreflightConAuthorizationSoloDesdeLaPwaConfigurada(string ruta)
    {
        const string origenPwa = "https://repartidor.example";
        using var host = new WebApplicationFactory<App>();
        using var server = host.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Repartidor:Origenes:0"] = origenPwa }));
        });
        using var client = server.CreateClient();

        using var permitido = await EnviarPreflightAsync(client, ruta, origenPwa);
        Assert.Equal(HttpStatusCode.NoContent, permitido.StatusCode);
        Assert.Equal(origenPwa, Assert.Single(permitido.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains(permitido.Headers.GetValues("Access-Control-Allow-Headers"),
            header => header.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(permitido.Headers.GetValues("Access-Control-Allow-Headers"),
            header => header.Contains("traceparent", StringComparison.OrdinalIgnoreCase));

        using var ajeno = await EnviarPreflightAsync(client, ruta, "https://otro.example");
        Assert.False(ajeno.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("GET", "/api/repartidor/avisos/clave")]
    [InlineData("POST", "/api/repartidor/avisos/suscripcion")]
    [InlineData("POST", "/api/repartidor/avisos/suscripcion/baja")]
    public async Task LosAvisosPushExigenElTokenDelRepartidor(string metodo, string ruta)
    {
        using var host = new WebApplicationFactory<App>();
        using var server = host.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
        using var client = server.CreateClient();
        using var solicitud = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (metodo == "POST") solicitud.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        using var respuesta = await client.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    private static async Task<HttpResponseMessage> EnviarPreflightAsync(HttpClient client, string ruta, string origen)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Options, ruta);
        solicitud.Headers.Add("Origin", origen);
        solicitud.Headers.Add("Access-Control-Request-Method", "GET");
        solicitud.Headers.Add("Access-Control-Request-Headers", "authorization,traceparent");
        return await client.SendAsync(solicitud);
    }
}
