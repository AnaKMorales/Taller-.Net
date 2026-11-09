using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using UltimaMilla.Infrastructure.Mensajeria;
using UltimaMilla.Infrastructure.Observabilidad;
using UltimaMilla.Web.Desarrollo;
using UltimaMilla.Web.Observabilidad;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task TelemetriaDeSincronizacionExigeSesionYRechazaConteosInvalidos()
    {
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false, ambiente: "Development");
        using var cliente = servidor.Cliente();
        using var creacion = await cliente.PostAsync("/dev/datos-demo-pwa", null);
        creacion.EnsureSuccessStatusCode();
        var demo = (await creacion.Content.ReadFromJsonAsync<DatosDemoCreados>())!;
        var reporte = new UltimaMilla.Web.Seguridad.ReporteSincronizacion(Guid.NewGuid(), 3, 1);
        using (var anonimo = await cliente.PostAsJsonAsync("/api/repartidor/telemetria", reporte))
            Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);
        using var ingreso = await cliente.PostAsJsonAsync("/api/repartidor/sesion",
            new { email = demo.EmailRepartidor, contrasena = demo.Contrasena });
        ingreso.EnsureSuccessStatusCode();
        var token = (await ingreso.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("accessToken").GetString();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        foreach (var invalido in new[]
        {
            reporte with { DispositivoId = Guid.Empty }, reporte with { Pendientes = -1 },
            reporte with { Rechazadas = -1 }, reporte with { Pendientes = 100001 }
        })
        {
            using var rechazo = await cliente.PostAsJsonAsync("/api/repartidor/telemetria", invalido);
            Assert.Equal(HttpStatusCode.BadRequest, rechazo.StatusCode);
        }
        using var valido = await cliente.PostAsJsonAsync("/api/repartidor/telemetria", reporte);
        Assert.Equal(HttpStatusCode.NoContent, valido.StatusCode);
    }

    [PostgresFact]
    public async Task SondeoDeBasePublicaConteosRealesDeOutboxYAvisosFallidos()
    {
        var demo = await DatosDemo.CrearAsync(Options(), CancellationToken.None, incluirVisitaParcial: true);
        var puntos = new System.Collections.Concurrent.ConcurrentDictionary<string, long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, owner) =>
            {
                if (instrument.Meter.Name == MetricasOperativas.NombreMeter)
                    owner.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => puntos[instrument.Name] = value);
        listener.Start();
        var opciones = new OpcionesRabbitMq();
        await using var rabbit = new ConexionRabbitMq(opciones);
        await new SondeoMetricasOperativas(_testConnection, rabbit, opciones).MedirBaseAsync(CancellationToken.None);
        await using var db = Context(demo.OperadorId);
        Assert.Equal(await db.Set<UltimaMilla.Infrastructure.Persistence.Outbox.MensajeOutbox>().LongCountAsync(),
            puntos["ultimamilla.outbox.pendientes"]);
        Assert.Equal(1, puntos["ultimamilla.avisos.fallidos"]);
    }
}

public sealed class SincronizacionesMetricasTests
{
    [Fact]
    public void MuestrasSeReemplazanPorDispositivoYVencenSinExportarIdentidades()
    {
        var reloj = new Reloj();
        using var estado = new EstadoSincronizaciones(reloj);
        var puntos = new Dictionary<string, long>();
        using var listener = new MeterListener();
        var publicando = false;
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (publicando && instrument.Meter.Name == EstadoSincronizaciones.NombreMeter)
                owner.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            Assert.Equal(0, tags.Length);
            puntos[instrument.Name] = value;
        });
        listener.Start();
        publicando = true;
        estado.Iniciar();
        publicando = false;
        var usuario = Guid.NewGuid();
        var dispositivo = Guid.NewGuid();
        Assert.True(estado.Registrar(usuario, dispositivo, 3, 1));
        Assert.True(estado.Registrar(usuario, Guid.NewGuid(), 2, 0));
        Assert.True(estado.Registrar(usuario, dispositivo, 1, 1));
        listener.RecordObservableInstruments();
        Assert.Equal(3, puntos["ultimamilla.sincronizaciones.pendientes"]);
        Assert.Equal(2, puntos["ultimamilla.sincronizaciones.dispositivos"]);
        reloj.Ahora = reloj.Ahora.AddMinutes(5);
        listener.RecordObservableInstruments();
        Assert.Equal(0, puntos["ultimamilla.sincronizaciones.pendientes"]);
        Assert.Equal(0, puntos["ultimamilla.sincronizaciones.dispositivos"]);
    }

    private sealed class Reloj : TimeProvider
    {
        public DateTimeOffset Ahora = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
