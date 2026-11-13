using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Despacho;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private async Task<(EscenarioPersistencia Escenario, Guid RutaId)> PrepararDespacho(bool cargar = true)
    {
        var e = Escenario();
        await using var db = Context();
        db.Add(e.Envio);
        await db.SaveChangesAsync();
        e.Recibir();
        var ruta = e.Asignar();
        db.Add(ruta);
        if (cargar)
            foreach (var b in e.Envio.Bultos)
                db.Add(EscaneoBulto.RegistrarCarga(ruta, b, e.Admin, EscenarioPersistencia.Fecha));
        await db.SaveChangesAsync();
        return (e, ruta.Id);
    }

    private DespachoRutaRepository RepositorioDespacho(EscenarioPersistencia e) => new(Options(),
        new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador)));
    private DespacharRutaHandler HandlerDespacho(EscenarioPersistencia e) => new(RepositorioDespacho(e),
        new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(1))));

    [PostgresFact]
    public async Task ApiDespacharRutaValidaPermisosAntiforgeryVersionYPublicaHojaAlRepartidor()
    {
        var (e, rutaId) = await PrepararDespacho();
        var ajeno = new EscenarioPersistencia();
        await using (var db = Context(ajeno.Operador.Id)) { db.Add(ajeno.Admin); await db.SaveChangesAsync(); }
        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin);
        using var repartidor = servidor.Cliente(e);
        using var anonimo = servidor.Cliente();
        using var otro = servidor.Cliente(ajeno, ajeno.Admin);
        ((RelojConfiguracion)servidor.Services.GetRequiredService<TimeProvider>()).Ahora = EscenarioPersistencia.Fecha.AddHours(1);
        var url = $"/api/despacho/rutas/{rutaId}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(url + "/preparacion")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(url + "/preparacion")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otro.GetAsync(url + "/preparacion")).StatusCode);
        var preparacion = (await admin.GetFromJsonAsync<PreparacionRutaDto>(url + "/preparacion", JsonApi))!;
        Assert.True(Assert.Single(preparacion.Bultos).Cargado);
        var solicitud = new DespacharRutaCommand(preparacion.Version);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url + "/despachar", solicitud)).StatusCode);
        await Antiforgery(admin);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url + "/despachar", new DespacharRutaCommand(Guid.NewGuid()))).StatusCode);
        using var respuesta = await admin.PostAsJsonAsync(url + "/despachar", solicitud);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = (await respuesta.Content.ReadFromJsonAsync<RutaDespachadaDto>(JsonApi))!;
        Assert.NotEqual(preparacion.Version, resultado.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url + "/despachar", solicitud)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url + "/despachar", new DespacharRutaCommand(resultado.Version))).StatusCode);
        var hojas = new HojasRuta(Options(), new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Repartidor.Id, RolUsuario.Repartidor)));
        Assert.Equal(rutaId, Assert.Single(await hojas.ListarAsync()).RutaId);
        await using var lectura = Context();
        Assert.Equal(EstadoEnvio.EnTransito, (await lectura.Envios.SingleAsync()).Estado);
        Assert.Single(await lectura.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.IniciarEntrega).ToListAsync());
    }

    [PostgresFact]
    public async Task DespachoConCargaIncompletaNoCambiaEstadoVersionNiOutbox()
    {
        var (e, rutaId) = await PrepararDespacho(cargar: false);
        var handler = HandlerDespacho(e);
        var antes = await handler.ConsultarAsync(rutaId);
        Assert.False(Assert.Single(antes.Bultos).Cargado);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(rutaId, new(antes.Version)));
        Assert.Equal(antes.Version, (await handler.ConsultarAsync(rutaId)).Version);
        await using var db = Context();
        Assert.Equal(EstadoEnvio.AsignadoARuta, (await db.Envios.SingleAsync()).Estado);
        Assert.Empty(await db.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.IniciarEntrega).ToListAsync());
    }

    [PostgresFact]
    public async Task DespachoFalloDeOutboxRevierteRutaEnviosYPermiteReintentar()
    {
        var (e, rutaId) = await PrepararDespacho();
        var handler = HandlerDespacho(e);
        var antes = await handler.ConsultarAsync(rutaId);
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" ADD CONSTRAINT \"FalloDespacho\" CHECK (false) NOT VALID");
        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(rutaId, new(antes.Version)));
        var despues = await handler.ConsultarAsync(rutaId);
        Assert.Null(despues.DespachadaEn);
        Assert.Equal(antes.Version, despues.Version);
        await using (var db = Context())
        {
            Assert.Equal(EstadoEnvio.AsignadoARuta, (await db.Envios.SingleAsync()).Estado);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" DROP CONSTRAINT \"FalloDespacho\"");
        }
        await handler.Handle(rutaId, new(antes.Version));
    }

    [PostgresFact]
    public async Task DespachoDeNuevaVisitaExigeSoloLaCargaDeLosBultosPendientes()
    {
        var (e, _) = await RegistrarPrimeraParcial();
        var reloj = new RelojConfiguracion { Ahora = EscenarioPersistencia.Fecha.AddHours(2) };
        var tenant = new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador));
        var pendientes = new DespachoPendientes(Options(), tenant, reloj);
        var plan = await pendientes.ConsultarAsync(e.Envio.Id, default);
        var ruta = await pendientes.CrearRutaAsync(new(e.Repartidor.Id, e.Vehiculo.Id, 10), default);
        await pendientes.AsignarAsync(ruta.Id, SolicitudNuevaVisita(plan, ruta), default);
        var handler = new DespacharRutaHandler(RepositorioDespacho(e), reloj);
        var antes = await handler.ConsultarAsync(ruta.Id);
        var bultoPendiente = Assert.Single(antes.Bultos);
        Assert.False(bultoPendiente.Cargado);
        var carga = new RegistrarCargaRutaHandler(RepositorioDespacho(e), reloj);
        await carga.Handle(ruta.Id, new(bultoPendiente.Codigo, antes.Version, bultoPendiente.BultoId));
        var cargada = await handler.ConsultarAsync(ruta.Id);
        Assert.True(Assert.Single(cargada.Bultos).Cargado);
        await handler.Handle(ruta.Id, new(cargada.Version));
        await using var lectura = Context(e.Operador.Id);
        var final = await lectura.Envios.Include(x => x.Bultos).SingleAsync();
        Assert.Equal(2, final.Bultos.Count(b => b.Estado == EstadoBulto.Entregado));
        Assert.Equal(1, final.Bultos.Count(b => b.Estado == EstadoBulto.EnTransito));
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, final.Estado);
    }

    [PostgresFact]
    public async Task DosDespachosSimultaneosConfirmanUnSoloJuegoDeEventos()
    {
        var (e, rutaId) = await PrepararDespacho();
        var repositorio = new BarreraDespacho(RepositorioDespacho(e));
        var handler = new DespacharRutaHandler(repositorio, new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(1))));
        var version = (await HandlerDespacho(e).ConsultarAsync(rutaId)).Version;
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => handler.Handle(rutaId, new(version))),
            Record.ExceptionAsync(() => handler.Handle(rutaId, new(version))));
        Assert.Single(errores, x => x is null);
        Assert.IsType<ConflictoConcurrenciaException>(Assert.Single(errores, x => x is not null));
        await using var db = Context();
        Assert.Single(await db.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.IniciarEntrega).ToListAsync());
    }

    private sealed class BarreraDespacho(IDespachoRutaRepository inner) : IDespachoRutaRepository
    {
        private int _abiertas;
        private readonly TaskCompletionSource _listas = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IOperacionDespachoRuta> AbrirAsync(Guid id, CancellationToken ct)
        {
            var operacion = await inner.AbrirAsync(id, ct);
            if (Interlocked.Increment(ref _abiertas) == 2) _listas.TrySetResult();
            try { await _listas.Task.WaitAsync(TimeSpan.FromSeconds(20), ct); return operacion; }
            catch { await operacion.DisposeAsync(); throw; }
        }
    }
}
