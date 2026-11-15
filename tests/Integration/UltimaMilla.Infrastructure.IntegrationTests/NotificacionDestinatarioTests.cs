using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Application.Avisos;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Notificaciones;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Notificaciones;
using UltimaMilla.Infrastructure.Persistence.Inbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

// Aviso al destinatario (requisito 27) con la identidad del operador (requisito 5): docs/notificaciones-destinatario.md.
public sealed partial class PostgresTests
{
    private sealed class EnviadorFalso(bool falla = false) : IEnviadorNotificaciones
    {
        public ConcurrentQueue<NotificacionSaliente> Enviadas { get; } = new();
        public bool Falla { get; set; } = falla;
        public Task EnviarAsync(NotificacionSaliente notificacion, CancellationToken cancellationToken = default)
        {
            if (Falla) throw new HttpRequestException("Proveedor caído");
            Enviadas.Enqueue(notificacion);
            return Task.CompletedTask;
        }
    }

    private NotificacionDestinatario Avisos(IEnviadorNotificaciones enviador) => new(Options(),
        new RegistroIdempotencia(Options(), new RelojPublicacion()), enviador,
        new OpcionesNotificaciones { UrlPublica = "https://ultimamilla.example" }, new RelojPublicacion(),
        NullLogger<NotificacionDestinatario>.Instance);

    // Evento del alta (Admitido, relevante) y del último cambio (EnDeposito, interno), como llegan por la cola.
    private async Task<(EventoEnvioV1 Alta, EventoEnvioV1 Recepcion, TenantContext Tenant, EscenarioPersistencia E)> EventosDelEnvioAsync()
    {
        var e = await GuardarEscenario(recibido: true);
        await using var db = Context();
        var envio = await db.Envios.Include(x => x.Eventos).SingleAsync();
        var eventos = envio.Eventos.OrderBy(x => x.OcurridoEn).ThenBy(x => x.EstadoNuevo).ToList();
        var cuenta = e.Envio.CuentaComercial.Id;
        EventoEnvioV1 Integracion(EventoDeEnvio v) => new(Guid.NewGuid(), 1, e.Operador.Id, cuenta, envio.Id, v.Id,
            v.Tipo.ToString(), v.Origen.ToString(), v.EstadoAnterior?.ToString(), v.EstadoNuevo.ToString(), v.OcurridoEn, v.IdOperacion);
        return (Integracion(eventos.First(v => v.EstadoNuevo == EstadoEnvio.Admitido)),
            Integracion(eventos.Last(v => v.EstadoNuevo == EstadoEnvio.EnDeposito)),
            TenantContext.ParaMensaje(e.Operador.Id, cuenta), e);
    }

    private async Task<List<Notificacion>> NotificacionesAsync()
    {
        await using var db = Context();
        return await db.Set<Notificacion>().AsNoTracking().ToListAsync();
    }

    [PostgresFact]
    public async Task AvisoAlDestinatarioSeEnviaUnaVezConLaMarcaDelOperadorYElEnlace()
    {
        var (alta, _, tenant, e) = await EventosDelEnvioAsync();
        var enviador = new EnviadorFalso();
        var avisos = Avisos(enviador);

        await avisos.ManejarAsync(alta, tenant);
        // El mismo mensaje otra vez (reentrega de la cola): no se repite el aviso.
        await avisos.ManejarAsync(alta, tenant);

        var enviada = Assert.Single(enviador.Enviadas);
        Assert.Equal((CanalNotificacion.Email, "persona@example.com"), (enviada.Canal, enviada.Destino));
        Assert.StartsWith(e.Operador.Marca + ": ", enviada.Asunto);
        Assert.Contains($"https://ultimamilla.example/seguimiento/{e.Envio.CodigoSeguimiento}", enviada.Cuerpo);
        var guardada = Assert.Single(await NotificacionesAsync());
        Assert.Equal((enviada.Cuerpo, CanalNotificacion.Email), (guardada.Mensaje, guardada.Canal));
        Assert.NotNull(guardada.EnviadaEn);
        // Y el backoffice lo muestra en el detalle del envío.
        await using var lectura = Context();
        var detalle = await new UltimaMilla.Infrastructure.Persistence.Backoffice.DetalleEnvio(lectura).ObtenerAsync(e.Envio.Id);
        Assert.Equal(enviada.Cuerpo, Assert.Single(detalle!.Notificaciones!).Mensaje);
    }

    [PostgresFact]
    public async Task AvisoAlDestinatarioNoSeEnviaPorCambiosInternos()
    {
        var (_, recepcion, tenant, _) = await EventosDelEnvioAsync();
        var enviador = new EnviadorFalso();
        await Avisos(enviador).ManejarAsync(recepcion, tenant);
        Assert.Empty(enviador.Enviadas);
        Assert.Empty(await NotificacionesAsync());
    }

    [PostgresFact]
    public async Task AvisoAlDestinatarioQueFallaNoQuedaRegistradoYSeReintentaUnaSolaVez()
    {
        var (alta, _, tenant, _) = await EventosDelEnvioAsync();
        var enviador = new EnviadorFalso(falla: true);
        var avisos = Avisos(enviador);

        await Assert.ThrowsAsync<HttpRequestException>(() => avisos.ManejarAsync(alta, tenant));
        Assert.Empty(await NotificacionesAsync());
        await using (var db = Context())
            Assert.False(await db.Set<MensajeProcesado>().AnyAsync(m => m.Consumidor == NotificacionDestinatario.Consumidor));

        enviador.Falla = false;
        await avisos.ManejarAsync(alta, tenant);
        Assert.Single(enviador.Enviadas);
        Assert.Single(await NotificacionesAsync());
    }

    [PostgresFact]
    public async Task AvisoAlDestinatarioNoSeDuplicaCuandoFallanLosWebhooksYElMensajeSeReintenta()
    {
        var (alta, _, tenant, e) = await EventosDelEnvioAsync();
        await SuscribirAsync(e, "https://comercio.example/hook", [TipoEvento.Admitir]);
        var enviador = new EnviadorFalso();
        var webhooks = Webhooks(new ClienteFalso(_ => ResultadoWebhook.Fallo("HTTP 500")));
        var manejador = new ManejadoresEventoEnvio([Avisos(enviador), webhooks]);

        // Dos intentos del mismo mensaje: los webhooks fallan las dos veces y el mensaje vuelve a la cola.
        await Assert.ThrowsAsync<AvisoWebhookFallidoException>(() => manejador.ManejarAsync(alta, tenant));
        await Assert.ThrowsAsync<AvisoWebhookFallidoException>(() => manejador.ManejarAsync(alta, tenant));

        Assert.Single(enviador.Enviadas);
        Assert.Single(await NotificacionesAsync());
        Assert.Equal(2, Assert.Single(await EntregasAsync()).Intentos);
    }

    [PostgresFact]
    public async Task AvisoAlDestinatarioConElTenantDeOtroOperadorNoVeElEnvio()
    {
        var (alta, _, _, e) = await EventosDelEnvioAsync();
        var enviador = new EnviadorFalso();
        // El resolver ya rechaza un operador inexistente; aun si llegara, ni siquiera se puede reservar el mensaje en
        // su nombre (ProcesadorAvisos lo descarta) y el envío de otro operador nunca se lee.
        await Assert.ThrowsAsync<UltimaMilla.Application.Abstractions.AislamientoOperadorException>(() =>
            Avisos(enviador).ManejarAsync(alta, TenantContext.ParaMensaje(Guid.NewGuid(), e.Envio.CuentaComercial.Id)));
        Assert.Empty(enviador.Enviadas);
        Assert.Empty(await NotificacionesAsync());
    }
}
