using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using UltimaMilla.Web.Components;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed class RutasApiInexistentesTests
{
    [Theory]
    [InlineData("/api/inexistente")]
    [InlineData("/api/repartidor/inexistente")]
    public async Task UnaRutaDeApiInexistenteDa404YNoElIndexDeLaPwa(string ruta)
    {
        using var host = new WebApplicationFactory<App>();
        using var server = host.WithWebHostBuilder(b => b.UseEnvironment("Testing"));
        using var client = server.CreateClient();

        using var respuesta = await client.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.NotEqual("text/html", respuesta.Content.Headers.ContentType?.MediaType);
    }
}
