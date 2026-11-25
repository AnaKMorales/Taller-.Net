using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using UltimaMilla.Web.Components;

namespace UltimaMilla.Infrastructure.IntegrationTests;

// Hallazgo H2 de la reentrega: la página heredada /Envios no tenía [Authorize] y listaba nombre, teléfono y dirección de
// todos los envíos del operador a cualquier rol. Se eliminó; el backoffice usa /backoffice/envios/{id} con su autorización.
public sealed class PaginaEnviosHeredadaTests
{
    [Theory]
    [InlineData("/Envios")]
    [InlineData("/envios")]
    [InlineData("/Envios/Index")]
    public async Task LaListaHeredadaDeEnviosYaNoExiste(string ruta)
    {
        using var host = new WebApplicationFactory<App>();
        using var server = host.WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        using var client = server.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var respuesta = await client.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.DoesNotContain("Teléfono", await respuesta.Content.ReadAsStringAsync());
    }
}
