using System.Text.Json;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    // Integra Domain, EF y outbox; no sustituye la prueba E2E de Web/PWA/Worker.
    // Cada etapa abre otro contexto para verificar el estado realmente persistido.
    [PostgresFact]
    public async Task FlujoEntregaConContextosIndependientesConservaHistorialOutboxYAislamiento()
    {
        var e = await GuardarEscenario();
        var fecha = EscenarioPersistencia.Fecha;
        var tenant = TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador);
        var operacion = Guid.NewGuid();
        Guid rutaId;

        await using (var db = Scoped(tenant))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            Assert.Equal(EstadoEnvio.Admitido, envio.Estado);
            Assert.All(envio.Bultos, b => Assert.Equal(EstadoBulto.Admitido, b.Estado));
            var admin = await db.Set<Usuario>().SingleAsync(x => x.Id == e.Admin.Id);
            foreach (var bulto in envio.Bultos)
                EscaneoBulto.RegistrarRecepcion(bulto, admin, fecha.AddMinutes(1));
            envio.RecibirEnDeposito(admin, fecha.AddMinutes(1));
            await db.SaveChangesAsync();
        }

        await using (var db = Scoped(tenant))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
            Assert.All(envio.Bultos, b => Assert.Equal(EstadoBulto.EnDeposito, b.Estado));
            var admin = await db.Set<Usuario>().SingleAsync(x => x.Id == e.Admin.Id);
            var vehiculo = new Vehiculo(await db.Set<Operador>().SingleAsync(), "FLUJO123", 100, 10);
            // La flota se registra antes de usarla en una asignación operativa.
            db.Add(vehiculo);
            await db.SaveChangesAsync();
            var ruta = new Ruta(await db.Set<Repartidor>().SingleAsync(), vehiculo, 10);
            ruta.AgregarParada(envio, 1, fecha.AddHours(1), admin, fecha.AddMinutes(2));
            db.Add(ruta);
            await db.SaveChangesAsync();
            rutaId = ruta.Id;
        }

        await using (var db = Scoped(tenant))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            Assert.Equal(EstadoEnvio.AsignadoARuta, envio.Estado);
            Assert.All(envio.Bultos, b => Assert.Equal(EstadoBulto.AsignadoARuta, b.Estado));
            var ruta = await CargarRuta(db, rutaId);
            var admin = await db.Set<Usuario>().SingleAsync(x => x.Id == e.Admin.Id);
            foreach (var bulto in envio.Bultos)
                EscaneoBulto.RegistrarCarga(ruta, bulto, admin, fecha.AddMinutes(3));
            ruta.Despachar(admin, fecha.AddMinutes(3));
            await db.SaveChangesAsync();
        }

        await using (var db = Scoped(tenant))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
            Assert.All(envio.Bultos, b => Assert.Equal(EstadoBulto.EnTransito, b.Estado));
            var repartidor = await db.Set<Repartidor>().SingleAsync();
            envio.RegistrarIntento(ResultadoIntento.Entregado, null, [e.Foto()], repartidor,
                fecha.AddHours(1), operacion);
            await db.SaveChangesAsync();
        }

        // Una repetición posterior, ya sin parada activa, no crea otro intento/evento/mensaje.
        await using (var db = Scoped(tenant))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            var repartidor = await db.Set<Repartidor>().SingleAsync();
            var intento = envio.RegistrarIntento(ResultadoIntento.Entregado, null, [], repartidor,
                fecha.AddHours(1), operacion);
            Assert.Equal(operacion, intento.IdOperacion);
            Assert.Equal(0, await db.SaveChangesAsync());
        }

        await using (var db = Scoped(tenant))
        {
            var ruta = await CargarRuta(db, rutaId);
            ruta.Rendir(fecha.AddHours(2));
            await db.SaveChangesAsync();
        }

        await using var reader = Scoped(tenant);
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(EstadoEnvio.Entregado, actual.Estado);
        Assert.All(actual.Bultos, b => Assert.Equal(EstadoBulto.Entregado, b.Estado));
        Assert.Null(actual.Parada);
        Assert.Equal(e.Envio.FechaComprometida, actual.FechaComprometida);
        Assert.Equal(e.Envio.TarifaCalculada, actual.TarifaCalculada);
        var intentoFinal = Assert.Single(actual.Intentos);
        Assert.Equal(ResultadoIntento.Entregado, intentoFinal.Resultado);
        Assert.Equal(new ArchivoRef("evidencias/foto.jpg"), Assert.Single(intentoFinal.Pruebas).ArchivoRef);
        Assert.Equal(new[] { TipoEscaneo.Recepcion, TipoEscaneo.Carga },
            actual.Bultos.Single().Escaneos.OrderBy(x => x.FechaHora).Select(x => x.Tipo));
        Assert.Equal(new[] { TipoEvento.Admitir, TipoEvento.Validar, TipoEvento.Asignar,
            TipoEvento.IniciarEntrega, TipoEvento.EntregaExitosa },
            actual.Eventos.OrderBy(x => x.OcurridoEn).Select(x => x.Tipo));
        Assert.Equal(rutaId, (await reader.Set<Rendicion>().SingleAsync()).RutaId);
        Assert.False((await reader.Set<Parada>().SingleAsync()).Activa);

        var mensajes = await reader.Set<MensajeOutbox>().ToListAsync();
        Assert.Equal(actual.Eventos.Select(x => x.Id).Order(), mensajes.Select(x => x.Id).Order());
        Assert.All(mensajes, mensaje =>
        {
            Assert.Null(mensaje.PublicadoEn);
            var payload = JsonSerializer.Deserialize<EventoEnvioV1>(mensaje.Contenido,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Equal(e.Operador.Id, payload.OperadorId);
            Assert.Equal(actual.CuentaComercialId, payload.CuentaComercialId);
            Assert.Equal(actual.Id, payload.EnvioId);
        });

        await using var ajeno = Context(Guid.NewGuid());
        Assert.Empty(await ajeno.Envios.ToListAsync());
        Assert.Empty(await ajeno.Set<MensajeOutbox>().ToListAsync());
        Assert.Empty(await ajeno.Set<Rendicion>().ToListAsync());
    }
}
