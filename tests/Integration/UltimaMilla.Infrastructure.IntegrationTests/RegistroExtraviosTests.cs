using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.Extravio;
using UltimaMilla.Infrastructure.Persistence.Envios;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private RegistrarExtravioHandler Extravios(EscenarioPersistencia e, DbContextOptions<AppDbContext>? opciones = null) =>
        new(new ExtravioRepository(opciones ?? Options(), TenantFlota(e)), new RelojConfiguracion());

    [PostgresFact]
    public async Task ExtravioApiGuardaEstadoHistorialYOutboxYLiberaParadaConVersionesNuevas()
    {
        var e = Escenario(); e.Recibir(); Ruta ruta;
        await using (var db = Context())
        {
            db.Add(e.Envio); await db.SaveChangesAsync();
            ruta = e.Asignar(); db.Add(ruta); await db.SaveChangesAsync();
            ruta.Despachar(e.Admin, EscenarioPersistencia.Fecha); await db.SaveChangesAsync();
        }
        using var servidor = new ServidorApi(_testConnection); using var admin = servidor.Cliente(e, e.Admin);
        using var anonimo = servidor.Cliente(); using var repartidor = servidor.Cliente(e);
        var url = $"/api/incidencias/envios/{e.Envio.Id}/extravio";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(url)).StatusCode);
        var previo = (await admin.GetFromJsonAsync<ConsultaExtravioDto>(url, JsonApi))!;
        var solicitud = new RegistrarExtravioCommand(previo.VersionEnvio, previo.VersionRuta, " No se encontró al verificar el vehículo ");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url, solicitud)).StatusCode);
        await Antiforgery(admin);
        using var respuesta = await admin.PostAsJsonAsync(url, solicitud);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<ExtravioRegistradoDto>(JsonApi))!;
        Assert.Equal(EstadoEnvio.Extraviado, resultado.Estado);
        Assert.NotEqual(previo.VersionEnvio, resultado.VersionEnvio); Assert.NotEqual(previo.VersionRuta, resultado.VersionRuta);
        Assert.Equal(ruta.Id, resultado.RutaAfectadaId); Assert.All(resultado.Bultos, b => Assert.Equal(EstadoBulto.Extraviado, b.Estado));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url, solicitud)).StatusCode);
        await using var lectura = Context();
        var envio = await lectura.Envios.Include(x => x.Bultos).Include(x => x.Parada).Include(x => x.Eventos).ThenInclude(v => v.Responsable).SingleAsync();
        Assert.Null(envio.Parada);
        var parada = await lectura.Set<Parada>().SingleAsync(); Assert.False(parada.Activa);
        var evento = Assert.Single(envio.Eventos, v => v.Tipo == TipoEvento.EventoAnomalo);
        Assert.Equal(resultado.EventoId, evento.Id); Assert.Equal(e.Admin.Id, evento.ResponsableId);
        Assert.Equal("No se encontró al verificar el vehículo", evento.Detalle);
        Assert.Equal(EscenarioPersistencia.Fecha, evento.OcurridoEn);
        Assert.Equal(1, await lectura.Set<MensajeOutbox>().CountAsync(m => m.Id == evento.Id));
        Assert.Equal(envio.Eventos.Count, await lectura.Set<MensajeOutbox>().CountAsync());
        var consulta = await Extravios(e).ConsultarAsync(e.Envio.Id); Assert.Null(consulta.RutaId); Assert.Null(consulta.VersionRuta);
    }

    [PostgresFact]
    public async Task ExtravioValidaDetalleVersionesYCronologiaYRechazaEstadoAdmitido()
    {
        var e = await PrepararAdministracionFlota(); var gestion = Extravios(e);
        var estado = await gestion.ConsultarAsync(e.Envio.Id);
        foreach (var solicitud in new[] { new RegistrarExtravioCommand(Guid.Empty, null, "Detalle"),
            new(estado.VersionEnvio, Guid.Empty, "Detalle"), new(estado.VersionEnvio, null, " "),
            new(estado.VersionEnvio, null, new string('x', 2001)) })
            await Assert.ThrowsAsync<ArgumentException>(() => gestion.Handle(e.Envio.Id, solicitud));
        await Assert.ThrowsAsync<ArgumentException>(() => gestion.ConsultarAsync(Guid.Empty));
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => gestion.Handle(e.Envio.Id, new(Guid.NewGuid(), null, "Detalle")));
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => gestion.Handle(e.Envio.Id, new(estado.VersionEnvio, Guid.NewGuid(), "Detalle")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gestion.Handle(e.Envio.Id, new(estado.VersionEnvio, null, "No permitido desde admitido")));
        await using var db = Context(); Assert.Single(await db.Set<EventoDeEnvio>().ToListAsync()); Assert.Single(await db.Set<MensajeOutbox>().ToListAsync());
    }

    [PostgresFact]
    public async Task ExtravioAislaOperadorYRevalidaActorSinAceptarRolesFalsificados()
    {
        var e = await PrepararAdministracionFlota(); var otro = new EscenarioPersistencia();
        await using (var db = Context(otro.Operador.Id)) { db.Add(otro.Envio); await db.SaveChangesAsync(); }
        var gestion = Extravios(e);
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => gestion.ConsultarAsync(otro.Envio.Id));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => gestion.Handle(otro.Envio.Id, new(Guid.NewGuid(), null, "Detalle")));
        var falso = new ExtravioRepository(Options(), new UltimaMilla.Application.Tenancy.TenantActual(
            UltimaMilla.Application.Tenancy.TenantContext.ParaUsuario(e.Operador.Id, e.Repartidor.Id, RolUsuario.Administrador)));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => falso.AbrirAsync(e.Envio.Id, default));
        await using (var db = Context()) { (await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id)).Desactivar(); await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => gestion.ConsultarAsync(e.Envio.Id));
    }

    [PostgresFact]
    public async Task ExtravioDePendientesConservaLosBultosEntregadosDeUnaEntregaParcial()
    {
        var e = await PrepararVisitaParcial();
        await using (var db = Context())
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            envio.RegistrarIntentoPorBulto(envio.Parada!.Id, SolicitudParcial(envio), await db.Set<Repartidor>().SingleAsync(),
                EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid()); await db.SaveChangesAsync();
        }
        var gestion = new RegistrarExtravioHandler(new ExtravioRepository(Options(), TenantFlota(e)),
            new RelojConfiguracion { Ahora = EscenarioPersistencia.Fecha.AddHours(2) });
        var previo = await gestion.ConsultarAsync(e.Envio.Id);
        var resultado = await gestion.Handle(e.Envio.Id, new(previo.VersionEnvio, previo.VersionRuta, "Pendiente no localizado"));
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, resultado.Estado);
        Assert.Equal(2, resultado.Bultos.Count(b => b.Estado == EstadoBulto.Entregado));
        Assert.Single(resultado.Bultos, b => b.Estado == EstadoBulto.Extraviado);
        await using var dbFinal = Context();
        Assert.Single(await dbFinal.Set<IntentoEntrega>().ToListAsync()); Assert.Equal(3, await dbFinal.Set<DetalleIntentoBulto>().CountAsync());
        Assert.Equal(1, await dbFinal.Set<MensajeOutbox>().CountAsync(m => m.Id == resultado.EventoId));
    }

    [PostgresFact]
    public async Task ExtravioConcurrenteSoloConfirmaUnaDecisionYUnMensaje()
    {
        var e = await GuardarEscenario(recibido: true);
        var normal = Extravios(e); var previo = await normal.ConsultarAsync(e.Envio.Id);
        var opciones = new DbContextOptionsBuilder<AppDbContext>(Options()).AddInterceptors(new BarreraAltaFlota()).Options;
        var gestion = Extravios(e, opciones); var solicitud = new RegistrarExtravioCommand(previo.VersionEnvio, null, "No localizado");
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => gestion.Handle(e.Envio.Id, solicitud)),
            Record.ExceptionAsync(() => gestion.Handle(e.Envio.Id, solicitud)));
        Assert.Single(errores, x => x is null); Assert.IsType<ConflictoConcurrenciaException>(Assert.Single(errores, x => x is not null));
        await using var db = Context(); var evento = await db.Set<EventoDeEnvio>().SingleAsync(v => v.Tipo == TipoEvento.EventoAnomalo);
        Assert.Equal(1, await db.Set<MensajeOutbox>().CountAsync(m => m.Id == evento.Id));
    }

    [PostgresFact]
    public async Task ExtravioFalloDelOutboxRevierteEstadoBultosParadaYVersiones()
    {
        var e = Escenario(); e.Recibir();
        await using (var db = Context())
        { db.Add(e.Envio); await db.SaveChangesAsync(); db.Add(e.Asignar()); await db.SaveChangesAsync(); }
        var gestion = Extravios(e); var previo = await gestion.ConsultarAsync(e.Envio.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => gestion.Handle(e.Envio.Id, new(previo.VersionEnvio, null, "Falta versión de ruta")));
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" ADD CONSTRAINT \"FalloExtravio\" CHECK (false) NOT VALID");
        await Assert.ThrowsAsync<DbUpdateException>(() => gestion.Handle(e.Envio.Id, new(previo.VersionEnvio, previo.VersionRuta, "Revertir")));
        var posterior = await gestion.ConsultarAsync(e.Envio.Id);
        Assert.Equal(previo.Estado, posterior.Estado); Assert.Equal(previo.VersionEnvio, posterior.VersionEnvio);
        Assert.Equal(previo.VersionRuta, posterior.VersionRuta); Assert.Equal(previo.RutaId, posterior.RutaId);
        Assert.Equal(previo.Bultos, posterior.Bultos);
        await using (var db = Context())
        {
            Assert.True((await db.Set<Parada>().SingleAsync()).Activa);
            Assert.False(await db.Set<EventoDeEnvio>().AnyAsync(v => v.Tipo == TipoEvento.EventoAnomalo));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" DROP CONSTRAINT \"FalloExtravio\"");
        }
        Assert.Equal(EstadoEnvio.Extraviado, (await gestion.Handle(e.Envio.Id, new(previo.VersionEnvio, previo.VersionRuta, "Reintento válido"))).Estado);
    }
}
