using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using UltimaMilla.Infrastructure.Mensajeria;
using UltimaMilla.Infrastructure.Observabilidad;

namespace UltimaMilla.Worker.Observabilidad;

public static class TelemetriaWorker
{
    public static void AgregarTelemetriaWorker(this HostApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, configuration) =>
        {
            var ajustes = services.GetRequiredService<IConfiguration>();
            configuration.ReadFrom.Configuration(ajustes).ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "UltimaMilla.Worker")
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .WriteTo.Console(new JsonFormatter(renderMessage: true));
            if (ajustes.GetValue<bool>("OpenTelemetry:ExportarOtlp"))
                configuration.WriteTo.OpenTelemetry(options =>
                {
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = "UltimaMilla.Worker",
                        ["deployment.environment.name"] = builder.Environment.EnvironmentName
                    };
                    options.OnBeginSuppressInstrumentation = SuppressInstrumentationScope.Begin;
                });
        }, preserveStaticLogger: true);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("UltimaMilla.Worker")
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(traces =>
            {
                traces.AddSource(DiagnosticoMensajeria.NombreFuente)
                    .AddHttpClientInstrumentation()
                    .AddProcessor(new OcultarUrlSaliente());
                if (builder.Configuration.GetValue<bool>("OpenTelemetry:ExportarOtlp"))
                    traces.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(MetricasOperativas.NombreMeter).AddHttpClientInstrumentation();
                if (builder.Configuration.GetValue<bool>("OpenTelemetry:ExportarOtlp"))
                    metrics.AddOtlpExporter();
            });
    }

    private sealed class OcultarUrlSaliente : BaseProcessor<Activity>
    {
        public override void OnEnd(Activity activity)
        {
            activity.SetTag("url.full", null);
            activity.SetTag("url.path", null);
            activity.SetTag("url.query", null);
            activity.SetTag("http.url", null);
            activity.SetTag("http.target", null);
        }
    }
}
