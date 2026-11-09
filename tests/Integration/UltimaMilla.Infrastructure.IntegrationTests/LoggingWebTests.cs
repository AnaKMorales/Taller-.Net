using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Core;
using Serilog.Events;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Web.Components;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed class LoggingWebTests
{
    [Fact]
    public async Task RegistraResultadoYTrazaSinExponerElTokenDeSeguimiento()
    {
        var registros = new CapturaLogs();
        using var host = new WebApplicationFactory<App>();
        using var servidor = host.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Serilog:MinimumLevel:Default"] = "Information"
                }));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILogEventSink>(registros);
                services.RemoveAll<ISeguimientoPublico>();
                services.AddSingleton<ISeguimientoPublico, SeguimientoSinDatos>();
            });
        });
        using var cliente = servidor.CreateClient();
        const string traceId = "0123456789abcdef0123456789abcdef";
        const string token = "token-privado-que-no-debe-aparecer";
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"/api/seguimiento/{token}?clave=secreta");
        solicitud.Headers.Add("traceparent", $"00-{traceId}-0123456789abcdef-01");
        using var respuesta = await cliente.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        using var vivo = await cliente.GetAsync("/live");
        Assert.Equal(HttpStatusCode.OK, vivo.StatusCode);

        var peticiones = registros.Eventos.Where(e => e.Properties.ContainsKey("Elapsed")).ToArray();
        Assert.Equal(2, peticiones.Length);
        var seguimiento = Assert.Single(peticiones, e => e.Properties["StatusCode"].ToString() == "404");
        Assert.Equal(traceId, ((ScalarValue)seguimiento.Properties["TraceId"]).Value);
        Assert.Equal("/api/seguimiento/{token}", ((ScalarValue)seguimiento.Properties["RequestPath"]).Value);
        Assert.Equal("UltimaMilla.Web", ((ScalarValue)seguimiento.Properties["Application"]).Value);
        Assert.Equal("Testing", ((ScalarValue)seguimiento.Properties["Environment"]).Value);
        var contenido = string.Join("\n", registros.Eventos.Select(e =>
            e.RenderMessage() + string.Join(" ", e.Properties.Values)));
        Assert.DoesNotContain(token, contenido);
        Assert.DoesNotContain("clave=secreta", contenido);
    }

    private sealed class CapturaLogs : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Eventos { get; } = new();
        public void Emit(LogEvent logEvent) => Eventos.Enqueue(logEvent);
    }

    private sealed class SeguimientoSinDatos : ISeguimientoPublico
    {
        public Task<SeguimientoPublicoDto?> ConsultarAsync(string token, CancellationToken cancellationToken = default) =>
            Task.FromResult<SeguimientoPublicoDto?>(null);
    }
}
