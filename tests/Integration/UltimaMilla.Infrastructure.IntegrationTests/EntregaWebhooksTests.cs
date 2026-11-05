using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Application.Avisos;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Avisos;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private sealed class ClienteFalso(Func<SolicitudWebhook, ResultadoWebhook> responder) : IClienteWebhook
    {
        public ConcurrentQueue<SolicitudWebhook> Enviados { get; } = new();
        public Task<ResultadoWebhook> EnviarAsync(SolicitudWebhook solicitud, CancellationToken cancellationToken = default)
        {
            Enviados.Enqueue(solicitud);
            return Task.FromResult(responder(solicitud));
        }
    }

    private EntregaWebhooks Webhooks(IClienteWebhook cliente) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_testConnection).Options, cliente,
        new RelojPublicacion(), NullLogger<EntregaWebhooks>.Instance);

    // Evento del último cambio del envío y su equivalente de integración (lo que llega por la cola).
    private async Task<(EventoEnvioV1 Evento, TenantContext Tenant, EscenarioPersistencia Escenario)> EventoRecibidoAsync()
    {
        var e = await GuardarEscenario(recibido: true);
        await using var db = Context();
        var envio = await db.Envios.Include(x => x.Eventos).SingleAsync();
        var ultimo = envio.Eventos.OrderBy(x => x.OcurridoEn).Last();
        var cuenta = e.Envio.CuentaComercial.Id;
        var evento = new EventoEnvioV1(ultimo.Id, 1, e.Operador.Id, cuenta, envio.Id, ultimo.Id, ultimo.Tipo.ToString(),
            ultimo.Origen.ToString(), ultimo.EstadoAnterior?.ToString(), ultimo.EstadoNuevo.ToString(), ultimo.OcurridoEn, ultimo.IdOperacion);
        return (evento, TenantContext.ParaMensaje(e.Operador.Id, cuenta), e);
    }

    private async Task<Guid> SuscribirAsync(EscenarioPersistencia e, string url, TipoEvento[] tipos, bool activa = true, string secreto = "secreto")
    {
        await using var db = Context();
        var cuenta = await db.Set<CuentaComercial>().SingleAsync();
        var suscripcion = new SuscripcionAviso(cuenta, tipos, secreto, new Uri(url));
        if (!activa) suscripcion.Desactivar();
        db.Add(suscripcion);
        await db.SaveChangesAsync();
        return suscripcion.Id;
    }

    private async Task<List<(Guid Suscripcion, bool Exitoso, int Intentos, DateTime? Ultimo)>> EntregasAsync()
    {
        await using var db = Context();
        return (await db.Set<EntregaAviso>().Include(x => x.Suscripcion).ToListAsync())
            .Select(x => (x.SuscripcionId, x.Exitoso, x.Intentos, x.UltimoIntentoEn)).ToList();
    }

    [PostgresFact]
    public async Task WebhookSoloVaALasSuscripcionesActivasQueCoincidenYRegistraElIntento()
    {
        var (evento, tenant, e) = await EventoRecibidoAsync();
        var tipo = Enum.Parse<TipoEvento>(evento.TipoEvento);
        var otroTipo = Enum.GetValues<TipoEvento>().First(t => t != tipo);
        var coincide = await SuscribirAsync(e, "https://uno.example/hook", [tipo, otroTipo], secreto: "secreto-uno");
        await SuscribirAsync(e, "https://dos.example/hook", [otroTipo]);                 // otro tipo de evento
        await SuscribirAsync(e, "https://tres.example/hook", [tipo], activa: false);     // inactiva
        var cliente = new ClienteFalso(s => ResultadoWebhook.Ok(200));

        await Webhooks(cliente).ManejarAsync(evento, tenant);

        var enviado = Assert.Single(cliente.Enviados);
        Assert.Equal("https://uno.example/hook", enviado.Destino.AbsoluteUri);
        Assert.Equal("secreto-uno", enviado.SecretoFirma);
        Assert.Equal(evento.EventoId, enviado.EventoId);
        // El cuerpo es el evento de integración (sin datos del destinatario).
        Assert.Contains($"\"mensajeId\":\"{evento.MensajeId}\"", enviado.Cuerpo);
        Assert.DoesNotContain("@", enviado.Cuerpo);
        var entrega = Assert.Single(await EntregasAsync());
        Assert.Equal(coincide, entrega.Suscripcion);
        Assert.True(entrega.Exitoso);
        Assert.Equal(1, entrega.Intentos);
        Assert.Equal(EscenarioPersistencia.Fecha.AddDays(20), entrega.Ultimo);
    }

    [PostgresFact]
    public async Task UnFalloRegistraElIntentoLanzaParaReintentarYElReintentoSoloRepiteLoQueFallo()
    {
        var (evento, tenant, e) = await EventoRecibidoAsync();
        var tipo = Enum.Parse<TipoEvento>(evento.TipoEvento);
        var buena = await SuscribirAsync(e, "https://buena.example/hook", [tipo]);
        var mala = await SuscribirAsync(e, "https://mala.example/hook", [tipo]);
        var malaResponde = false;
        var cliente = new ClienteFalso(s => s.Destino.Host == "mala.example" && !malaResponde
            ? ResultadoWebhook.Fallo("HTTP 500", 500) : ResultadoWebhook.Ok(200));
        var manejador = Webhooks(cliente);

        // 1º intento: una suscripción falla, la otra no se ve afectada, y se pide reintento.
        var error = await Assert.ThrowsAsync<AvisoWebhookFallidoException>(() => manejador.ManejarAsync(evento, tenant));
        Assert.Equal([mala], error.Suscripciones);
        Assert.Equal(2, cliente.Enviados.Count);
        var primeras = await EntregasAsync();
        Assert.True(primeras.Single(x => x.Suscripcion == buena).Exitoso);
        var fallida = primeras.Single(x => x.Suscripcion == mala);
        Assert.False(fallida.Exitoso);
        Assert.Equal(1, fallida.Intentos);

        // 2º intento (vuelve por la cola de espera): sigue fallando; la exitosa NO se reenvía.
        await Assert.ThrowsAsync<AvisoWebhookFallidoException>(() => manejador.ManejarAsync(evento, tenant));
        Assert.Equal(3, cliente.Enviados.Count);
        Assert.Equal("mala.example", cliente.Enviados.Last().Destino.Host);
        Assert.Equal(2, (await EntregasAsync()).Single(x => x.Suscripcion == mala).Intentos);

        // 3º intento: el comercio ya responde. Queda entregado y no lanza.
        malaResponde = true;
        await manejador.ManejarAsync(evento, tenant);
        var finales = await EntregasAsync();
        Assert.All(finales, x => Assert.True(x.Exitoso));
        Assert.Equal(3, finales.Single(x => x.Suscripcion == mala).Intentos);
        Assert.Equal(1, finales.Single(x => x.Suscripcion == buena).Intentos);

        // Un mensaje duplicado más no vuelve a enviar nada.
        var antes = cliente.Enviados.Count;
        await manejador.ManejarAsync(evento, tenant);
        Assert.Equal(antes, cliente.Enviados.Count);
    }

    [PostgresFact]
    public async Task UnaExcepcionInesperadaDelClienteCuentaComoFalloSinPerderElRegistro()
    {
        var (evento, tenant, e) = await EventoRecibidoAsync();
        await SuscribirAsync(e, "https://x.example/hook", [Enum.Parse<TipoEvento>(evento.TipoEvento)]);
        var cliente = new ClienteFalso(_ => throw new InvalidOperationException("boom"));

        await Assert.ThrowsAsync<AvisoWebhookFallidoException>(() => Webhooks(cliente).ManejarAsync(evento, tenant));

        var entrega = Assert.Single(await EntregasAsync());
        Assert.False(entrega.Exitoso);
        Assert.Equal(1, entrega.Intentos);
    }

    [PostgresFact]
    public async Task NoSeEnvianWebhooksDeOtraCuentaNiDeOtroOperador()
    {
        var (evento, tenant, e) = await EventoRecibidoAsync();
        await SuscribirAsync(e, "https://uno.example/hook", [Enum.Parse<TipoEvento>(evento.TipoEvento)]);
        var cliente = new ClienteFalso(_ => ResultadoWebhook.Ok(200));

        // Mismo evento pero un tenant de otro operador: no ve el evento ni las suscripciones.
        var ajeno = TenantContext.ParaMensaje(Guid.NewGuid(), Guid.NewGuid());
        await Webhooks(cliente).ManejarAsync(evento, ajeno);

        Assert.Empty(cliente.Enviados);
        Assert.Empty(await EntregasAsync());
    }

    [PostgresFact]
    public async Task UnTipoDeEventoDesconocidoNoEnviaNada()
    {
        var (evento, tenant, e) = await EventoRecibidoAsync();
        await SuscribirAsync(e, "https://uno.example/hook", [Enum.Parse<TipoEvento>(evento.TipoEvento)]);
        var cliente = new ClienteFalso(_ => ResultadoWebhook.Ok(200));
        await Webhooks(cliente).ManejarAsync(evento with { TipoEvento = "Inventado" }, tenant);
        Assert.Empty(cliente.Enviados);
    }
}
