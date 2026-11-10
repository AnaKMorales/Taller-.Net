using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Despacho;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task ApiDespachoAsignaSoloElPendienteYValidaPermisosVersionesYCapacidad()
    {
        var (e, intento) = await RegistrarPrimeraParcial();
        var pendiente = Assert.Single(intento.Bultos, b => b.Resultado == ResultadoIntento.Fallido).BultoId;
        var ajeno = new EscenarioPersistencia();
        await using (var db = Context(ajeno.Operador.Id))
        {
            db.Add(ajeno.Admin);
            await db.SaveChangesAsync();
        }
        await using (var db = Context(e.Operador.Id))
        {
            var operador = await db.Set<Operador>().SingleAsync();
            db.AddRange(new Vehiculo(operador, "SOLO-PENDIENTE", 1, 0.001m),
                new Vehiculo(operador, "SIN-CAPACIDAD", 0.5m, 0.001m));
            await db.SaveChangesAsync();
        }

        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin);
        using var repartidor = servidor.Cliente(e);
        using var anonimo = servidor.Cliente();
        using var otroOperador = servidor.Cliente(ajeno, ajeno.Admin);
        ((RelojConfiguracion)servidor.Services.GetRequiredService<TimeProvider>()).Ahora = EscenarioPersistencia.Fecha.AddHours(2);
        var consulta = $"/api/despacho/envios/{e.Envio.Id}/pendientes";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(consulta)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(consulta)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otroOperador.GetAsync(consulta)).StatusCode);
        var plan = (await admin.GetFromJsonAsync<PlanificacionPendientesDto>(consulta, JsonApi))!;
        Assert.Empty(plan.Rutas); // La ruta de la primera visita ya fue despachada.
        var flota = Assert.Single(plan.Repartidores);
        var pequeno = Assert.Single(plan.Vehiculos, v => v.Matricula == "SOLO-PENDIENTE");
        var insuficiente = Assert.Single(plan.Vehiculos, v => v.Matricula == "SIN-CAPACIDAD");
        var alta = new CrearRutaPendientes(flota.Id, pequeno.Id, 5);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/despacho/rutas", alta, JsonApi)).StatusCode);
        await Antiforgery(admin);
        var ruta = await CrearRutaPendientesApi(admin, alta);
        var rutaInsuficiente = await CrearRutaPendientesApi(admin, alta with { VehiculoId = insuficiente.Id });
        Assert.Contains((await admin.GetFromJsonAsync<PlanificacionPendientesDto>(consulta, JsonApi))!.Rutas, r => r.Id == ruta.Id);

        var franja = Assert.Single(plan.Franjas);
        var fecha = DateOnly.FromDateTime(EscenarioPersistencia.Fecha.AddDays(1));
        var inicio = new DateTimeOffset(fecha.ToDateTime(franja.Desde), TimeSpan.FromHours(-3));
        var fin = new DateTimeOffset(fecha.ToDateTime(franja.Hasta), TimeSpan.FromHours(-3));
        var solicitud = new AsignarPendientes(e.Envio.Id, plan.VersionEnvio, ruta.Version, ruta.ProximoOrden, inicio.AddMinutes(5),
            new ProgramacionPendientes(franja.Id, fecha, inicio, fin));
        var destino = $"/api/despacho/rutas/{ruta.Id}/pendientes";
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(destino,
            solicitud with { VersionEsperadaRuta = Guid.NewGuid() }, JsonApi)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(destino,
            solicitud with { VersionEsperadaEnvio = Guid.NewGuid() }, JsonApi)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/despacho/rutas/{rutaInsuficiente.Id}/pendientes",
            solicitud with { VersionEsperadaRuta = rutaInsuficiente.Version }, JsonApi)).StatusCode);
        var despuesDelFallo = (await admin.GetFromJsonAsync<PlanificacionPendientesDto>(consulta, JsonApi))!;
        Assert.Equal(plan.VersionEnvio, despuesDelFallo.VersionEnvio);

        using var respuesta = await admin.PostAsJsonAsync(destino, solicitud, JsonApi);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var visita = (await respuesta.Content.ReadFromJsonAsync<VisitaPendientesDto>(JsonApi))!;
        Assert.Equal(pendiente, Assert.Single(visita.Bultos));
        Assert.Equal(1m, visita.PesoKg);
        Assert.Equal(0.001m, visita.VolumenM3);
        Assert.NotEqual(plan.VersionEnvio, visita.VersionEnvio);
        Assert.NotEqual(ruta.Version, visita.VersionRuta);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(destino, solicitud, JsonApi)).StatusCode);

        await using var lectura = Context(e.Operador.Id);
        var envio = await EscenarioPersistencia.Grafo(lectura).SingleAsync();
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.Estado);
        Assert.Equal(2, envio.Bultos.Count(b => b.Estado == EstadoBulto.Entregado));
        Assert.Equal(visita.ParadaId, envio.Parada!.Id);
        Assert.Equal(2, await lectura.Set<Parada>().CountAsync()); // Solo la histórica y la confirmada.
        Assert.Equal(2, await lectura.Set<MensajeOutbox>().Include(m => m.Evento).CountAsync(m =>
            m.Tipo == "envio.evento.v2" && (m.Evento.Tipo == TipoEvento.Reintentar || m.Evento.Tipo == TipoEvento.Reasignar)));
    }

    [PostgresFact]
    public async Task ApiDespachoDevuelveSoloPendientesConLaPoliticaDelEnvio()
    {
        var e = await PrepararVisitaParcial(true, AccionBultosPendientes.DevolverAlComercio);
        var intento = await ComandoRegistro(e);
        await RegistroHandler(await IdentidadRepartidor(e)).Handle(intento);
        var pendiente = Assert.Single(intento.Bultos, b => b.Resultado == ResultadoIntento.Fallido).BultoId;

        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin);
        ((RelojConfiguracion)servidor.Services.GetRequiredService<TimeProvider>()).Ahora = EscenarioPersistencia.Fecha.AddHours(2);
        await Antiforgery(admin);
        var ruta = $"/api/despacho/envios/{e.Envio.Id}";
        var plan = (await admin.GetFromJsonAsync<PlanificacionPendientesDto>($"{ruta}/pendientes", JsonApi))!;
        Assert.Equal(AccionBultosPendientes.DevolverAlComercio, plan.Accion);

        using var respuesta = await admin.PostAsJsonAsync($"{ruta}/devolucion-pendientes",
            new { versionEsperadaEnvio = plan.VersionEnvio }, JsonApi);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var devolucion = (await respuesta.Content.ReadFromJsonAsync<DevolucionPendientesDto>(JsonApi))!;
        Assert.Equal(pendiente, Assert.Single(devolucion.Bultos));
        Assert.NotEqual(plan.VersionEnvio, devolucion.VersionEnvio);
        await using var lectura = Context(e.Operador.Id);
        var envio = await EscenarioPersistencia.Grafo(lectura).SingleAsync();
        Assert.Equal(2, envio.Bultos.Count(b => b.Estado == EstadoBulto.Entregado));
        Assert.Equal(EstadoBulto.EnDevolucion, envio.Bultos.Single(b => b.Id == pendiente).Estado);
    }

    private static async Task<RutaPlanificableDto> CrearRutaPendientesApi(HttpClient admin, CrearRutaPendientes alta)
    {
        using var respuesta = await admin.PostAsJsonAsync("/api/despacho/rutas", alta, JsonApi);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<RutaPlanificableDto>(JsonApi))!;
    }

    [PostgresFact]
    public async Task AsignacionesSimultaneasDePendientesSoloConfirmanUnaVisitaYUnJuegoDeEventos()
    {
        var (e, _) = await RegistrarPrimeraParcial();
        var reloj = new RelojConfiguracion { Ahora = EscenarioPersistencia.Fecha.AddHours(2) };
        var tenant = new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador));
        var servicio = new DespachoPendientes(Options(), tenant, reloj);
        var plan = await servicio.ConsultarAsync(e.Envio.Id, CancellationToken.None);
        var rutaA = await servicio.CrearRutaAsync(new(e.Repartidor.Id, e.Vehiculo.Id, 5), CancellationToken.None);
        var rutaB = await servicio.CrearRutaAsync(new(e.Repartidor.Id, e.Vehiculo.Id, 5), CancellationToken.None);
        var solicitud = SolicitudNuevaVisita(plan, rutaA);
        int eventosPrevios;
        await using (var db = Context(e.Operador.Id))
            eventosPrevios = await db.Set<MensajeOutbox>().CountAsync();
        var barrera = new BarreraAsignaciones();
        var opciones = new DbContextOptionsBuilder<AppDbContext>(Options()).AddInterceptors(barrera).Options;
        var concurrente = new DespachoPendientes(opciones, tenant, reloj);

        var resultados = await Task.WhenAll(Asignar(rutaA), Asignar(rutaB));

        Assert.Equal(1, resultados.Count(r => r));
        await using var lectura = Context(e.Operador.Id);
        var envio = await EscenarioPersistencia.Grafo(lectura).SingleAsync();
        Assert.NotNull(envio.Parada);
        Assert.Equal(1, await lectura.Set<Parada>().CountAsync(p => p.Activa));
        Assert.Equal(2, await lectura.Set<Parada>().CountAsync());
        Assert.Equal(eventosPrevios + 2, await lectura.Set<MensajeOutbox>().CountAsync());
        Assert.Equal(2, envio.Bultos.Count(b => b.Estado == EstadoBulto.Entregado));

        async Task<bool> Asignar(RutaPlanificableDto ruta)
        {
            try
            {
                await concurrente.AsignarAsync(ruta.Id, solicitud with { VersionEsperadaRuta = ruta.Version }, CancellationToken.None);
                return true;
            }
            catch (ConflictoConcurrenciaException) { return false; }
        }
    }

    [PostgresFact]
    public async Task ApiDespachoFalloDelOutboxRevierteLaVisitaYPermiteReintentarConLasMismasVersiones()
    {
        var (e, _) = await RegistrarPrimeraParcial();
        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin);
        ((RelojConfiguracion)servidor.Services.GetRequiredService<TimeProvider>()).Ahora = EscenarioPersistencia.Fecha.AddHours(2);
        await Antiforgery(admin);
        var consulta = $"/api/despacho/envios/{e.Envio.Id}/pendientes";
        var plan = (await admin.GetFromJsonAsync<PlanificacionPendientesDto>(consulta, JsonApi))!;
        var ruta = await CrearRutaPendientesApi(admin, new(e.Repartidor.Id, e.Vehiculo.Id, 5));
        var solicitud = SolicitudNuevaVisita(plan, ruta);
        var destino = $"/api/despacho/rutas/{ruta.Id}/pendientes";
        int eventosPrevios;
        await using (var db = Context(e.Operador.Id))
        {
            eventosPrevios = await db.Set<MensajeOutbox>().CountAsync();
            await db.Database.ExecuteSqlRawAsync(
                """ALTER TABLE "MensajesOutbox" ADD CONSTRAINT "FalloApiDespacho" CHECK ("Tipo" <> 'envio.evento.v2') NOT VALID""");
        }

        using var fallo = await admin.PostAsJsonAsync(destino, solicitud, JsonApi);
        Assert.Equal(HttpStatusCode.InternalServerError, fallo.StatusCode);
        await using (var db = Context(e.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            Assert.Null(envio.Parada);
            Assert.Equal(plan.VersionEnvio, db.Entry(envio).Property<Guid>("RowVersion").CurrentValue);
            Assert.Equal(ruta.Version, db.Entry(await CargarRuta(db, ruta.Id)).Property<Guid>("RowVersion").CurrentValue);
            Assert.Equal(EstadoBulto.NoEntregado, Assert.Single(envio.Bultos, b => b.Estado != EstadoBulto.Entregado).Estado);
            Assert.Equal(1, await db.Set<Parada>().CountAsync());
            Assert.Equal(eventosPrevios, await db.Set<MensajeOutbox>().CountAsync());
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "MensajesOutbox" DROP CONSTRAINT "FalloApiDespacho" """);
        }
        using var reintento = await admin.PostAsJsonAsync(destino, solicitud, JsonApi);
        Assert.Equal(HttpStatusCode.Created, reintento.StatusCode);
    }

    private static AsignarPendientes SolicitudNuevaVisita(PlanificacionPendientesDto plan, RutaPlanificableDto ruta)
    {
        var franja = Assert.Single(plan.Franjas);
        var fecha = DateOnly.FromDateTime(EscenarioPersistencia.Fecha.AddDays(1));
        var inicio = new DateTimeOffset(fecha.ToDateTime(franja.Desde), TimeSpan.FromHours(-3));
        var fin = new DateTimeOffset(fecha.ToDateTime(franja.Hasta), TimeSpan.FromHours(-3));
        return new(plan.EnvioId, plan.VersionEnvio, ruta.Version, ruta.ProximoOrden, inicio.AddMinutes(5),
            new(franja.Id, fecha, inicio, fin));
    }

    private sealed class BarreraAsignaciones : SaveChangesInterceptor
    {
        private int _llegadas;
        private readonly TaskCompletionSource _ambas = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _llegadas) == 2) _ambas.TrySetResult();
            await _ambas.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }
}
