using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Portal;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Backoffice;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task PanelDeAvisosMuestraYGestionaSoloLosWebhooksDelComercio()
    {
        var e = Escenario();
        await using (var db = Context())
        {
            db.Add(e.Envio);
            e.Recibir();
            var suscripcion = new SuscripcionAviso(e.Envio.CuentaComercial, [TipoEvento.Admitir, TipoEvento.Validar],
                "secreto", new Uri("https://comercio.example/avisos"));
            new EntregaAviso(suscripcion, e.Envio.Eventos.First()).RegistrarIntento(true, EscenarioPersistencia.Fecha);
            _ = new EntregaAviso(suscripcion, e.Envio.Eventos.Last());
            await db.SaveChangesAsync();
        }

        SuscripcionCreada creada;
        await using (var db = ContextoComercio(e, e.Envio.CuentaComercial.Id))
        {
            var panel = new PanelAvisos(db);
            var existente = Assert.Single(await panel.SuscripcionesAsync());
            Assert.Equal(("https://comercio.example/avisos", 1, 0, 1),
                (existente.Destino, existente.Entregados, existente.Fallidos, existente.Pendientes));

            var avisos = await panel.UltimosAvisosAsync(10);
            Assert.Equal(2, avisos.Count);
            Assert.Equal(e.Envio.CodigoSeguimiento, avisos[0].CodigoSeguimiento);
            Assert.Contains(avisos, a => a.Evento == TipoEvento.Admitir && a.Estado == EstadoAviso.Entregado);
            Assert.Contains(avisos, a => a.Evento == TipoEvento.Validar && a.Estado == EstadoAviso.Pendiente);

            creada = await panel.CrearSuscripcionAsync(new Uri("https://comercio.example/nuevo"), [TipoEvento.FalloEntrega]);
            Assert.Matches("^whsec_[0-9a-f]{64}$", creada.Secreto);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                panel.CrearSuscripcionAsync(new Uri("ftp://comercio.example/"), [TipoEvento.FalloEntrega]));
        }

        await using (var db = ContextoComercio(e, e.Envio.CuentaComercial.Id))
        {
            var panel = new PanelAvisos(db);
            Assert.Equal(2, (await panel.SuscripcionesAsync()).Count(s => s.Activa));
            Assert.True(await panel.DesactivarSuscripcionAsync(creada.Id));
        }

        await using (var db = Context())
        {
            var guardada = await db.Set<SuscripcionAviso>().SingleAsync(s => s.Id == creada.Id);
            Assert.False(guardada.Activa);
            Assert.Equal(creada.Secreto, guardada.SecretoFirma);
        }

        // Otra cuenta del mismo operador no ve ni toca nada.
        await using (var otra = ContextoComercio(e, Guid.NewGuid()))
        {
            var panel = new PanelAvisos(otra);
            Assert.Empty(await panel.SuscripcionesAsync());
            Assert.Empty(await panel.UltimosAvisosAsync(10));
            Assert.False(await panel.DesactivarSuscripcionAsync(creada.Id));
        }

        // Un usuario interno no tiene cuenta comercial: el panel no es para él.
        await using (var interno = Context())
        {
            var panel = new PanelAvisos(interno);
            Assert.Empty(await panel.SuscripcionesAsync());
            await Assert.ThrowsAsync<AislamientoOperadorException>(() =>
                panel.CrearSuscripcionAsync(new Uri("https://comercio.example/x"), [TipoEvento.Admitir]));
        }
    }

    [PostgresFact]
    public async Task ElAvisoParaReenviarArmaElEventoV1YSoloLoVeLaCuentaDuenia()
    {
        var e = Escenario();
        Guid fallido, entregado;
        await using (var db = Context())
        {
            db.Add(e.Envio);
            e.Recibir();
            var suscripcion = new SuscripcionAviso(e.Envio.CuentaComercial, [TipoEvento.Admitir, TipoEvento.Validar],
                "secreto", new Uri("https://comercio.example/avisos"));
            var ok = new EntregaAviso(suscripcion, e.Envio.Eventos.First());
            ok.RegistrarIntento(true, EscenarioPersistencia.Fecha);
            var mal = new EntregaAviso(suscripcion, e.Envio.Eventos.Last());
            mal.RegistrarIntento(false, EscenarioPersistencia.Fecha);
            (entregado, fallido) = (ok.Id, mal.Id);
            await db.SaveChangesAsync();
        }

        await using (var db = ContextoComercio(e, e.Envio.CuentaComercial.Id))
        {
            var panel = new PanelAvisos(db);
            var aviso = await panel.AvisoParaReenviarAsync(fallido);
            Assert.NotNull(aviso);
            Assert.False(aviso.Entregado);
            var validar = e.Envio.Eventos.Last();
            Assert.Equal(new EventoEnvioV1(Guid.Empty, 1, e.Operador.Id, e.Envio.CuentaComercial.Id, e.Envio.Id, validar.Id,
                "Validar", validar.Origen.ToString(), "Admitido", "EnDeposito", validar.OcurridoEn, null), aviso.Evento);
            Assert.True((await panel.AvisoParaReenviarAsync(entregado))!.Entregado);
            Assert.Null(await panel.AvisoParaReenviarAsync(Guid.NewGuid()));
        }

        // Otra cuenta del mismo operador y un usuario interno no lo obtienen.
        await using (var otra = ContextoComercio(e, Guid.NewGuid()))
            Assert.Null(await new PanelAvisos(otra).AvisoParaReenviarAsync(fallido));
        await using (var interno = Context())
            Assert.Null(await new PanelAvisos(interno).AvisoParaReenviarAsync(fallido));
    }

    private AppDbContext ContextoComercio(EscenarioPersistencia e, Guid cuentaId) =>
        new(Options(), new TenantActual(TenantContext.ParaUsuario(e.Operador.Id,
            e.Envio.CuentaComercial.Usuarios.Single().Id, RolUsuario.Comercio, cuentaId)));
}
