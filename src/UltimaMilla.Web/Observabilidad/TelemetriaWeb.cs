using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using UltimaMilla.Infrastructure.Cache;

namespace UltimaMilla.Web.Observabilidad;

public static class TelemetriaWeb
{
    public static void AgregarTelemetriaWeb(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("UltimaMilla.Web")
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(traces =>
            {
                traces.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddProcessor(new RutasSinDatosPersonales());
                if (builder.Configuration.GetValue<bool>("OpenTelemetry:ExportarOtlp")) traces.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(CacheMetricas.NombreMeter)
                    .AddMeter(EstadoSincronizaciones.NombreMeter)
                    .AddView("http.server.request.duration", new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
                    });
                if (builder.Configuration.GetValue<bool>("OpenTelemetry:ExportarOtlp")) metrics.AddOtlpExporter();
            });
    }

    private sealed class RutasSinDatosPersonales : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            // El seguimiento público incluye un secreto en el path. Las URLs salientes
            // también pueden tener datos personales, por eso conservamos solo el destino.
            activity.SetTag("url.path", activity.Kind == ActivityKind.Server
                ? activity.GetTagItem("http.route")?.ToString() ?? "[sin ruta]"
                : null);
            activity.SetTag("url.query", null);
            activity.SetTag("url.full", null);
            activity.SetTag("http.url", null);
            activity.SetTag("http.target", null);
        }
    }
}
