using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Cache;
using UltimaMilla.Web.Components;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed class TelemetriaWebTests
{
    [Fact]
    public async Task ExportaTrazaMetricaHttpYCacheSinElTokenPublico()
    {
        var trazas = new CapturaTrazas();
        var metricas = new CapturaMetricas();
        using var host = new WebApplicationFactory<App>();
        using var servidor = host.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISeguimientoPublico>();
                services.AddSingleton<ISeguimientoPublico, SeguimientoSinDatos>();
                services.AddOpenTelemetry()
                    .WithTracing(tracing => tracing.AddProcessor(new SimpleActivityExportProcessor(trazas)))
                    .WithMetrics(metrics => metrics.AddReader(new PeriodicExportingMetricReader(metricas,
                        exportIntervalMilliseconds: int.MaxValue)));
            });
        });
        using var cliente = servidor.CreateClient();
        const string traceId = "abcdef0123456789abcdef0123456789";
        const string secreto = "token-seguimiento-privado";
        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"/api/seguimiento/{secreto}?clave=privada");
        peticion.Headers.Add("traceparent", $"00-{traceId}-0123456789abcdef-01");
        using var respuesta = await cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        using var destino = new TcpListener(IPAddress.Loopback, 0);
        destino.Start();
        var puerto = ((IPEndPoint)destino.LocalEndpoint).Port;
        var recepcion = RecibirPeticion(destino);
        using (var origen = new Activity("llamada-prueba").SetParentId($"00-{traceId}-0123456789abcdef-01").Start())
        using (var externo = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
        using (var salida = await externo.GetAsync($"http://127.0.0.1:{puerto}/{secreto}?clave=privada"))
            Assert.Equal(HttpStatusCode.OK, salida.StatusCode);
        var cabeceras = await recepcion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains(cabeceras, h => h.StartsWith("traceparent:", StringComparison.OrdinalIgnoreCase) &&
            h.Contains(traceId, StringComparison.Ordinal));
        servidor.Services.GetRequiredService<CacheMetricas>().Acierto("tarifario");
        Assert.True(servidor.Services.GetRequiredService<TracerProvider>().ForceFlush());
        Assert.True(servidor.Services.GetRequiredService<MeterProvider>().ForceFlush());

        var traza = Assert.Single(trazas.Trazas, a => a.Kind == ActivityKind.Server && a.TraceId.ToString() == traceId);
        Assert.Equal("/api/seguimiento/{token}", traza.GetTagItem("url.path"));
        Assert.Equal("/api/seguimiento/{token}", traza.GetTagItem("http.route"));
        var atributos = string.Join(" ", traza.TagObjects.Select(t => t.Value?.ToString()));
        Assert.DoesNotContain(secreto, atributos);
        Assert.DoesNotContain("clave=privada", atributos);
        var trazaSaliente = Assert.Single(trazas.Trazas, a => a.Kind == ActivityKind.Client && a.TraceId.ToString() == traceId);
        var atributosSalientes = string.Join(" ", trazaSaliente.TagObjects.Select(t => t.Value?.ToString()));
        Assert.DoesNotContain(secreto, atributosSalientes);
        Assert.DoesNotContain("clave=privada", atributosSalientes);
        Assert.Contains(metricas.Puntos, m => m.Nombre == "http.server.request.duration" &&
            m.Ruta == "/api/seguimiento/{token}" && m.Cantidad > 0);
        Assert.Contains(metricas.Puntos, m => m.Nombre == "ultimamilla.cache.aciertos" && m.Cantidad > 0);
    }

    private static async Task<List<string>> RecibirPeticion(TcpListener listener)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var conexion = await listener.AcceptTcpClientAsync(cancellation.Token);
        await using var stream = conexion.GetStream();
        using var reader = new StreamReader(stream, leaveOpen: true);
        var cabeceras = new List<string>();
        while (await reader.ReadLineAsync(cancellation.Token) is { Length: > 0 } linea)
            cabeceras.Add(linea);
        await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(),
            cancellation.Token);
        return cabeceras;
    }

    private sealed class CapturaTrazas : BaseExporter<Activity>
    {
        public ConcurrentQueue<Activity> Trazas { get; } = new();
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch) Trazas.Enqueue(activity);
            return ExportResult.Success;
        }
    }

    private sealed record Punto(string Nombre, string? Ruta, long Cantidad);

    private sealed class CapturaMetricas : BaseExporter<Metric>
    {
        public ConcurrentQueue<Punto> Puntos { get; } = new();
        public override ExportResult Export(in Batch<Metric> batch)
        {
            foreach (var metric in batch)
            {
                if (metric.Name is not ("http.server.request.duration" or "ultimamilla.cache.aciertos")) continue;
                foreach (ref readonly var point in metric.GetMetricPoints())
                {
                    string? ruta = null;
                    foreach (var tag in point.Tags)
                        if (tag.Key == "http.route") ruta = tag.Value?.ToString();
                    Puntos.Enqueue(new(metric.Name, ruta, metric.Name == "http.server.request.duration"
                        ? point.GetHistogramCount() : point.GetSumLong()));
                }
            }
            return ExportResult.Success;
        }
    }

    private sealed class SeguimientoSinDatos : ISeguimientoPublico
    {
        public Task<SeguimientoPublicoDto?> ConsultarAsync(string token, CancellationToken cancellationToken = default) =>
            Task.FromResult<SeguimientoPublicoDto?>(null);
    }
}
