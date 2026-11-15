using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using UltimaMilla.Application.Avisos;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Portal;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Avisos;
using UltimaMilla.Infrastructure.Persistence.Backoffice;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private sealed class TransporteReenvioV2 : ITransporteMensajes
    {
        public MensajeParaPublicar? Mensaje { get; private set; }
        public Task PublicarConfirmadoAsync(MensajeParaPublicar mensaje, CancellationToken cancellationToken = default)
        { Mensaje = mensaje; return Task.CompletedTask; }
    }

    [PostgresFact]
    public async Task WebhooksV1YV2LleganPorHttpFirmadosYConservanElIntentoEnReintentoYReenvio()
    {
        var e = await PrepararVisitaParcial();
        EventoEnvioV2 original;
        await using (var db = Context())
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            envio.RegistrarIntentoPorBulto(envio.Parada!.Id, SolicitudParcial(envio), await db.Set<Repartidor>().SingleAsync(),
                EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid());
            await db.SaveChangesAsync();
            var outbox = await db.Set<MensajeOutbox>().SingleAsync(m => m.Tipo == EventoEnvioV2.TipoMensaje);
            original = JsonSerializer.Deserialize<EventoEnvioV2>(outbox.Contenido, JsonApi)!;
            // Cambiar el bulto pendiente después del evento no debe alterar su aviso histórico.
            envio.MarcarExtraviado(await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id),
                EscenarioPersistencia.Fecha.AddHours(2), "Pendiente extraviado después del intento");
            await db.SaveChangesAsync();
        }
        var recibidos = new ConcurrentQueue<(int Version, string Cuerpo, bool FirmaValida)>();
        var intentosV2 = 0;
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var receptor = builder.Build();
        receptor.MapPost("/{version:int}", async (HttpRequest request, int version) =>
        {
            var cuerpo = await new StreamReader(request.Body).ReadToEndAsync();
            var reloj = new RelojPublicacion().GetUtcNow();
            var valida = FirmaWebhook.Verificar("secreto", request.Headers[FirmaWebhook.EncabezadoTimestamp], cuerpo,
                request.Headers[FirmaWebhook.EncabezadoFirma], reloj, TimeSpan.FromSeconds(30));
            recibidos.Enqueue((version, cuerpo, valida));
            return Results.StatusCode(!valida ? 400 : version == 2 && Interlocked.Increment(ref intentosV2) <= 2 ? 503 : 200);
        });
        await receptor.StartAsync();
        var direccion = receptor.Urls.Single();
        try
        {
            await using (var db = Context())
            {
                var cuenta = await db.Set<CuentaComercial>().SingleAsync();
                var tipo = Enum.Parse<TipoEvento>(original.TipoEvento);
                db.Add(new SuscripcionAviso(cuenta, [tipo], "secreto", new Uri(direccion + "/1")));
                db.Add(new SuscripcionAviso(cuenta, [tipo], "secreto", new Uri(direccion + "/2"), 2));
                await db.SaveChangesAsync();
            }
            var v1 = new EventoEnvioV1(original.MensajeId, 1, original.OperadorId, original.CuentaComercialId,
                original.EnvioId, original.EventoId, original.TipoEvento, original.Origen, original.EstadoAnterior,
                original.EstadoNuevo, original.OcurridoEn, original.IdOperacion);
            var tenant = TenantContext.ParaMensaje(e.Operador.Id, original.CuentaComercialId);
            using var cliente = new ClienteWebhookHttp(new() { PermitirDestinosPrivados = true });
            var entrega = Webhooks(cliente);
            await Assert.ThrowsAsync<AvisoWebhookFallidoException>(() => entrega.ManejarAsync(v1, tenant));
            await Assert.ThrowsAsync<AvisoWebhookFallidoException>(() => entrega.ManejarAsync(v1, tenant));
            Assert.All(recibidos, r => Assert.True(r.FirmaValida));
            Assert.Equal(JsonSerializer.Serialize(v1, JsonApi), Assert.Single(recibidos, r => r.Version == 1).Cuerpo);
            Assert.All(recibidos.Where(r => r.Version == 2), r => Assert.Equal(JsonSerializer.Serialize(original, JsonApi), r.Cuerpo));

            // El reenvío manual sigue publicando V1 internamente; V2 reconstruye los mismos detalles históricos.
            var transporte = new TransporteReenvioV2();
            await using (var db = ContextoComercio(e, e.Envio.CuentaComercialId))
            {
                var panel = new PanelAvisos(db);
                var fallido = Assert.Single(await panel.UltimosAvisosAsync(10), a => a.Estado == EstadoAviso.Fallido);
                Assert.Equal(ResultadoReenvio.Reenviado, await new ReenviarAvisoHandler(panel, transporte, new RelojPublicacion()).Handle(fallido.Id));
            }
            var reenviado = JsonSerializer.Deserialize<EventoEnvioV1>(transporte.Mensaje!.Contenido, JsonApi)!;
            Assert.NotEqual(v1.MensajeId, reenviado.MensajeId);
            await entrega.ManejarAsync(reenviado, tenant);
            Assert.Equal(JsonSerializer.Serialize(original with { MensajeId = reenviado.MensajeId }, JsonApi), recibidos.Last().Cuerpo);
            Assert.Equal(4, recibidos.Count); // una entrega V1, tres intentos V2
            await entrega.ManejarAsync(reenviado, tenant);
            Assert.Equal(4, recibidos.Count);
            Assert.All(await EntregasAsync(), x => Assert.True(x.Exitoso));
            var ajeno = new ClienteFalso(_ => ResultadoWebhook.Ok(200));
            await Webhooks(ajeno).ManejarAsync(v1, TenantContext.ParaMensaje(Guid.NewGuid(), Guid.NewGuid()));
            Assert.Empty(ajeno.Enviados);
        }
        finally { await receptor.StopAsync(); }
    }

    [PostgresFact]
    public async Task WebhookV2SinIntentoNoInventaBultosYElPanelPermiteOptarPorVersion()
    {
        var (evento, tenant, e) = await EventoRecibidoAsync();
        await using (var db = ContextoComercio(e, e.Envio.CuentaComercialId))
        {
            var panel = new PanelAvisos(db);
            var tipo = Enum.Parse<TipoEvento>(evento.TipoEvento);
            await panel.CrearSuscripcionAsync(new Uri("https://v1.example/hook"), [tipo]);
            await panel.CrearSuscripcionAsync(new Uri("https://v2.example/hook"), [tipo], 2);
            Assert.Equal(new[] { 1, 2 }, (await panel.SuscripcionesAsync()).Select(s => s.VersionContrato).Order());
            await Assert.ThrowsAsync<ArgumentException>(() => panel.CrearSuscripcionAsync(new Uri("https://otro.example/hook"), [tipo], 3));
        }
        var cliente = new ClienteFalso(_ => ResultadoWebhook.Ok(200));
        await Webhooks(cliente).ManejarAsync(evento, tenant);
        var v2 = JsonSerializer.Deserialize<EventoEnvioV2>(cliente.Enviados.Single(x => x.Destino.Host == "v2.example").Cuerpo, JsonApi)!;
        Assert.Equal(2, v2.Version); Assert.Empty(v2.Bultos); Assert.Null(v2.IdOperacion);
    }

    [PostgresFact]
    public async Task PortalDeAvisosCreaSuscripcionV2DesdeFormularioConCsrf()
    {
        var e = await PrepararApiComercios();
        using var servidor = ServidorComercios(new()); using var cliente = servidor.Cliente();
        await LoginApiComercio(cliente);
        Assert.Contains("VersionContrato", await cliente.GetStringAsync("/portal/avisos"));
        static FormUrlEncodedContent Formulario(string version) => new(new Dictionary<string, string>
        {
            ["_handler"] = "crear", ["Destino"] = "https://receptor.example/hook",
            ["Eventos"] = "EntregaParcial", ["VersionContrato"] = version
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync("/portal/avisos", Formulario("2"))).StatusCode);
        await Antiforgery(cliente);
        using var respuesta = await cliente.PostAsync("/portal/avisos", Formulario("2"));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        await using var db = ContextoComercio(e, e.Envio.CuentaComercialId);
        Assert.Equal(2, Assert.Single(await new PanelAvisos(db).SuscripcionesAsync()).VersionContrato);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync("/portal/avisos", Formulario("3"))).StatusCode);
        Assert.Single(await new PanelAvisos(db).SuscripcionesAsync());
    }

    [PostgresFact]
    public async Task MigracionWebhookMantieneSuscripcionesPreviasEnV1YRechazaVersionesInvalidas()
    {
        var (_, _, e) = await EventoRecibidoAsync();
        var id = await SuscribirAsync(e, "https://previa.example/hook", [TipoEvento.Validar]);
        await using (var db = Context())
        {
            await db.GetService<IMigrator>().MigrateAsync("20261115190814_AgregarIdentidad");
            await db.GetService<IMigrator>().MigrateAsync();
            Assert.Equal(1, (await db.Set<SuscripcionAviso>().SingleAsync(s => s.Id == id)).VersionContrato);
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"SuscripcionesAviso\" SET \"VersionContrato\" = 3 WHERE \"Id\" = {id}"));
        }
    }
}
