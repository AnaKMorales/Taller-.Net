using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Web.Components;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed class CabecerasReenviadasTests
{
    private const string RedConfiable = "10.1.0.0/16";

    [Fact]
    public async Task ConXForwardedProtoHttpsDesdeUnaIpConfiableElPedidoSeVeComoHttps()
    {
        var visto = await EnviarAsync(ipOrigen: "10.1.0.5", redConfiable: RedConfiable);

        Assert.Equal("https", visto.Esquema);
        Assert.Equal(IPAddress.Parse("203.0.113.9"), visto.IpRemota); // X-Forwarded-For también se respeta
    }

    [Fact]
    public async Task ConXForwardedProtoHttpsDesdeUnaIpNoConfiableLaCabeceraSeIgnora()
    {
        var visto = await EnviarAsync(ipOrigen: "198.51.100.7", redConfiable: RedConfiable);

        Assert.Equal("http", visto.Esquema);
        Assert.Equal(IPAddress.Parse("198.51.100.7"), visto.IpRemota);
    }

    [Fact]
    public async Task SinRedConfiguradaSoloConfiaEnLoopback()
    {
        Assert.Equal("https", (await EnviarAsync("127.0.0.1", redConfiable: null)).Esquema);
        Assert.Equal("http", (await EnviarAsync("10.1.0.5", redConfiable: null)).Esquema);
    }

    [Fact]
    public void UnCidrInvalidoFallaConUnMensajeClaro()
    {
        var error = Assert.Throws<FormatException>(() => CabecerasReenviadas.LeerRedes("172.28.0.0/16, no-es-cidr"));
        Assert.Contains("no-es-cidr", error.Message);
    }

    private static async Task<Visto> EnviarAsync(string ipOrigen, string? redConfiable)
    {
        var registro = new Registro();
        using var host = new WebApplicationFactory<App>();
        using var server = host.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                if (redConfiable is not null)
                    config.AddInMemoryCollection(new Dictionary<string, string?> { [CabecerasReenviadas.ClaveRedConfiable] = redConfiable });
            });
            builder.ConfigureServices(servicios =>
            {
                servicios.AddSingleton(registro);
                servicios.AddTransient<IStartupFilter, SimularOrigen>();
            });
        });
        using var client = server.CreateClient();

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/no-existe");
        solicitud.Headers.Add(SimularOrigen.CabeceraIp, ipOrigen);
        solicitud.Headers.Add("X-Forwarded-Proto", "https");
        solicitud.Headers.Add("X-Forwarded-For", "203.0.113.9");
        using var respuesta = await client.SendAsync(solicitud);

        return registro.Ultimo ?? throw new InvalidOperationException("El pedido no llegó a la tubería.");
    }

    private sealed record Visto(string Esquema, IPAddress? IpRemota);

    private sealed class Registro
    {
        public Visto? Ultimo { get; set; }
    }

    // TestServer no trae IP de origen: este filtro (el más externo) la fija desde una cabecera de prueba y,
    // cuando el pedido ya pasó por UseForwardedHeaders, anota lo que la aplicación terminó viendo.
    private sealed class SimularOrigen(Registro registro) : IStartupFilter
    {
        public const string CabeceraIp = "X-Test-Ip";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, siguiente) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(context.Request.Headers[CabeceraIp].ToString());
                await siguiente();
                registro.Ultimo = new Visto(context.Request.Scheme, context.Connection.RemoteIpAddress);
            });
            next(app);
        };
    }
}
