extern alias Pwa;

using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Infrastructure.Mensajeria;
using UltimaMilla.Infrastructure.Observabilidad;
using UltimaMilla.Infrastructure.Persistence.Outbox;
using UltimaMilla.Web.Desarrollo;
using Pwa::UltimaMilla.Repartidor.Dispositivo;
using Pwa::UltimaMilla.Repartidor.Servidor;
using Pwa::UltimaMilla.Repartidor.Simulacion;
using Pwa::UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed class ColectorFactAttribute : FactAttribute
{
    public ColectorFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ULTIMAMILLA_TEST_COLECTOR")) ||
            string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ULTIMAMILLA_TEST_CONNECTION_STRING")) ||
            string.IsNullOrEmpty(RabbitMqFactAttribute.Variable("CLAVE")))
            Skip = "Requiere PostgreSQL, RabbitMQ y el compose local de observabilidad.";
    }
}

public sealed partial class PostgresTests
{
    [ColectorFact]
    public async Task ColectorRealRecibeTrazaPwaApiOutboxRabbitYMetricasDelTablero()
    {
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false,
            ambiente: "Development", exportarTelemetria: true);
        using var web = servidor.Cliente();
        using var creacion = await web.PostAsync("/dev/datos-demo-pwa", null);
        creacion.EnsureSuccessStatusCode();
        var demo = (await creacion.Content.ReadFromJsonAsync<DatosDemoCreados>())!;
        var almacen = new AlmacenPwa();
        using var conexionDispositivo = new EstadoConexion(new Navegador(new JsSinNavegadorPwa()));
        var api = new ApiRepartidorHttp(web, new ApiRepartidorFalsa(almacen, conexionDispositivo), TimeProvider.System);
        var sesion = (await api.IniciarSesionAsync(demo.EmailRepartidor, demo.Contrasena))!;
        var hoja = await api.ObtenerHojaDeRutaAsync(sesion);
        var visita = VisitaDemoPwa(Assert.Single(hoja.Paradas), DateTimeOffset.UtcNow);
        var cola = new ColaAcciones(almacen);
        Assert.Empty(await cola.EncolarAsync(visita, hoja, sesion.RepartidorId));
        var accion = Assert.Single(await cola.ListarAsync(sesion.RepartidorId)).Accion;
        Assert.True(ActivityContext.TryParse(accion.TraceParent, null, out var contexto));
        await api.ReportarSincronizacionAsync(sesion, new(Guid.NewGuid(), 1, 0));
        Assert.Equal(1, (await new Sincronizador(cola, api, TimeProvider.System).SincronizarAsync(sesion)).Sincronizadas);

        var opciones = new OpcionesRabbitMq
        {
            Host = RabbitMqFactAttribute.Variable("HOST") ?? "127.0.0.1",
            Puerto = int.Parse(RabbitMqFactAttribute.Variable("PUERTO")!),
            Usuario = RabbitMqFactAttribute.Variable("USUARIO") ?? "ultimamilla",
            Clave = RabbitMqFactAttribute.Variable("CLAVE")!,
            Exchange = "trace." + Guid.NewGuid().ToString("N"),
            ColaAvisos = "trace." + Guid.NewGuid().ToString("N")
        };
        await using var rabbit = new ConexionRabbitMq(opciones);
        var topologia = new TopologiaRabbitMq(rabbit, opciones);
        await topologia.DeclararAsync();
        using var trazasWorker = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("UltimaMilla.Worker"))
            .AddSource(DiagnosticoMensajeria.NombreFuente).AddOtlpExporter().Build();
        using var metricasWorker = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("UltimaMilla.Worker"))
            .AddMeter(MetricasOperativas.NombreMeter).AddOtlpExporter().Build();
        await using var db = Context(demo.OperadorId);
        var outbox = await db.Set<MensajeOutbox>().SingleAsync(m => m.Evento.IdOperacion == accion.IdOperacion);
        var procesado = new TaskCompletionSource<ActivityContext>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var consumidor = new ConsumidorRabbitMq(rabbit, topologia, opciones,
            new CapturarContexto(outbox.Id, procesado), NullLogger<ConsumidorRabbitMq>.Instance);
        try
        {
            var sondeo = new SondeoMetricasOperativas(_testConnection, rabbit, opciones);
            await sondeo.MedirBaseAsync(CancellationToken.None);
            await sondeo.MedirColasAsync(CancellationToken.None);
            await consumidor.IniciarAsync();
            await using var transporte = new TransporteRabbitMq(rabbit, opciones);
            await using var escritura = Context(demo.OperadorId);
            Assert.True(await Publicador(escritura, transporte).PublicarLoteAsync() > 0);
            Assert.Equal(contexto.TraceId, (await procesado.Task.WaitAsync(TimeSpan.FromSeconds(10))).TraceId);
            await consumidor.DetenerAsync();
            servidor.Services.GetRequiredService<UltimaMilla.Infrastructure.Cache.CacheMetricas>().Acierto("tarifario");
            Assert.True(servidor.Services.GetRequiredService<TracerProvider>().ForceFlush());
            Assert.True(servidor.Services.GetRequiredService<MeterProvider>().ForceFlush());
            Assert.True(trazasWorker.ForceFlush());
            Assert.True(metricasWorker.ForceFlush());

            using var consultas = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var tempo = Environment.GetEnvironmentVariable("ULTIMAMILLA_TEST_TEMPO") ?? "http://127.0.0.1:13200";
            var prometheus = Environment.GetEnvironmentVariable("ULTIMAMILLA_TEST_PROMETHEUS") ?? "http://127.0.0.1:19090";
            var grafana = Environment.GetEnvironmentVariable("ULTIMAMILLA_TEST_GRAFANA") ?? "http://127.0.0.1:3300";
            string traza = "";
            for (var i = 0; i < 30; i++)
            {
                using var respuesta = await consultas.GetAsync($"{tempo}/api/traces/{contexto.TraceId}");
                if (respuesta.IsSuccessStatusCode)
                {
                    traza = await respuesta.Content.ReadAsStringAsync();
                    if (traza.Contains("avisos process", StringComparison.Ordinal) &&
                        traza.Contains("UltimaMilla.Web", StringComparison.Ordinal)) break;
                }
                await Task.Delay(1000);
            }
            Assert.Contains("avisos process", traza);
            Assert.Contains("UltimaMilla.Web", traza);
            Assert.Contains("UltimaMilla.Worker", traza);
            string logs = "";
            var consultaLogs = Uri.EscapeDataString("{service_name=\"UltimaMilla.Web\"} | trace_id=\"" + contexto.TraceId + "\"");
            for (var i = 0; i < 30; i++)
            {
                logs = await consultas.GetStringAsync($"{grafana}/api/datasources/proxy/uid/loki/loki/api/v1/query_range?query={consultaLogs}");
                using var registros = JsonDocument.Parse(logs);
                if (registros.RootElement.GetProperty("data").GetProperty("result").GetArrayLength() > 0) break;
                await Task.Delay(1000);
            }
            using (var registros = JsonDocument.Parse(logs))
                Assert.True(registros.RootElement.GetProperty("data").GetProperty("result").GetArrayLength() > 0,
                    "Loki debe recibir el log de la petición con el identificador de traza de la PWA.");
            using var metrica = JsonDocument.Parse(await consultas.GetStringAsync(
                $"{prometheus}/api/v1/query?query=ultimamilla_mensajes_procesados_total"));
            Assert.True(metrica.RootElement.GetProperty("data").GetProperty("result").GetArrayLength() > 0);
            using var tablero = JsonDocument.Parse(await consultas.GetStringAsync($"{grafana}/api/dashboards/uid/ultimamilla-tecnico"));
            Assert.Contains(tablero.RootElement.GetProperty("dashboard").GetProperty("panels").EnumerateArray(),
                panel => panel.GetProperty("type").GetString() == "logs");
            foreach (var panel in tablero.RootElement.GetProperty("dashboard").GetProperty("panels").EnumerateArray())
            {
                if (panel.GetProperty("type").GetString() != "timeseries") continue;
                foreach (var consulta in panel.GetProperty("targets").EnumerateArray())
                {
                    var expresion = consulta.GetProperty("expr").GetString()!;
                    using var resultado = await consultas.GetAsync($"{prometheus}/api/v1/query?query={Uri.EscapeDataString(expresion)}");
                    Assert.True(resultado.IsSuccessStatusCode, expresion + " " + await resultado.Content.ReadAsStringAsync());
                }
            }
        }
        finally
        {
            await consumidor.DetenerAsync();
            await using var channel = await (await rabbit.ObtenerAsync()).CreateChannelAsync();
            await channel.ExchangeDeleteAsync(opciones.Exchange);
            foreach (var nombre in new[] { opciones.ColaAvisos, opciones.ColaMuertos }
                .Concat(opciones.Esperas.Select(e => opciones.ColaEspera(e.Sufijo))))
                await channel.QueueDeleteAsync(nombre);
        }
    }

    private sealed class CapturarContexto(Guid id, TaskCompletionSource<ActivityContext> resultado) : IManejadorMensajes
    {
        public Task<ResultadoConsumo> ManejarAsync(MensajeRecibido mensaje, CancellationToken cancellationToken = default)
        {
            if (mensaje.MensajeId == id.ToString()) resultado.TrySetResult(Activity.Current!.Context);
            return Task.FromResult(ResultadoConsumo.Confirmar);
        }
    }
}
