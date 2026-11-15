using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Deposito;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Despacho;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private async Task<(EscenarioPersistencia E, RutaPlanificableDto Ruta, Guid SegundoId)> PrepararAsignacionRegular(
        bool recibir = true, decimal? capacidadPeso = null, decimal? capacidadVolumen = null, int maxParadas = 10)
    {
        var e = Escenario();
        var segundo = new Envio(e.Envio.CuentaComercial,
            new("Otra", "099000003", "otra@example.com", new("Calle", "2", "Montevideo", "11000")),
            [new("SEGUNDO", 2, 10, 20, 30)], e.Envio.ModalidadServicio, e.Zona, e.Reglas, e.Envio.CuadroTarifario, 150,
            EscenarioPersistencia.Fecha, e.Envio.FechaComprometida, e.Envio.Programacion, e.Admin);
        var vehiculo = capacidadPeso is null && capacidadVolumen is null ? e.Vehiculo :
            new Vehiculo(e.Operador, "CAPACIDAD", capacidadPeso ?? 100, capacidadVolumen ?? 10);
        await using (var db = Context())
        {
            db.AddRange(e.Envio, segundo, e.Repartidor, vehiculo);
            await db.SaveChangesAsync();
            if (recibir)
            {
                foreach (var envio in new[] { e.Envio, segundo })
                {
                    foreach (var bulto in envio.Bultos) EscaneoBulto.RegistrarRecepcion(bulto, e.Admin, EscenarioPersistencia.Fecha);
                    envio.RecibirEnDeposito(e.Admin, EscenarioPersistencia.Fecha);
                }
                await db.SaveChangesAsync();
            }
        }
        var rutas = new DespachoPendientes(Options(), TenantAsignacion(e), RelojAsignacion());
        var ruta = await rutas.CrearRutaAsync(new(e.Repartidor.Id, vehiculo.Id, maxParadas), default);
        return (e, ruta, segundo.Id);
    }

    private static TenantActual TenantAsignacion(EscenarioPersistencia e) =>
        new(TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador));
    private static RelojFijo RelojAsignacion() => new(new DateTimeOffset(EscenarioPersistencia.Fecha.AddMinutes(30)));
    private AsignacionEnvioRutaRepository RepositorioAsignacion(EscenarioPersistencia e) => new(Options(), TenantAsignacion(e));
    private AsignarEnvioRutaHandler HandlerAsignacion(EscenarioPersistencia e) => new(RepositorioAsignacion(e), RelojAsignacion());
    private static AsignarEnvioRutaCommand SolicitudAsignacion(EnvioAsignableDto envio, RutaAsignacionDto ruta, int? orden = null) =>
        new(envio.Id, envio.Version, ruta.Version, orden ?? ruta.ProximoOrden, new DateTimeOffset(envio.VentanaDesde).AddHours(2));

    [PostgresFact]
    public async Task ApiAsignacionRegularIntegraRecepcionCargaDespachoYOrdenDeHoja()
    {
        var (e, rutaCreada, segundoId) = await PrepararAsignacionRegular(recibir: false);
        Usuario despachador;
        await using (var db = Context())
        {
            despachador = new Usuario(await db.Set<Operador>().SingleAsync(), "despachador@example.com", RolUsuario.Despachador);
            db.Add(despachador); await db.SaveChangesAsync();
        }
        using var servidor = new ServidorApi(_testConnection);
        using var cliente = servidor.Cliente(e, despachador);
        ((RelojConfiguracion)servidor.Services.GetRequiredService<TimeProvider>()).Ahora = EscenarioPersistencia.Fecha.AddMinutes(30);
        await Antiforgery(cliente);
        var recursos = (await cliente.GetFromJsonAsync<RecursosAsignacionDto>("/api/despacho/recursos", JsonApi))!;
        Assert.Equal(e.Repartidor.Id, Assert.Single(recursos.Repartidores).Id);
        Assert.Equal(e.Vehiculo.Id, Assert.Single(recursos.Vehiculos).Id);
        Assert.Empty((await cliente.GetFromJsonAsync<EnvioAsignableDto[]>("/api/despacho/envios/disponibles", JsonApi))!);
        foreach (var id in new[] { e.Envio.Id, segundoId })
        {
            var recepcion = $"/api/deposito/envios/{id}/recepcion";
            var estado = (await cliente.GetFromJsonAsync<RecepcionDepositoDto>(recepcion, JsonApi))!;
            foreach (var bulto in estado.Bultos)
            {
                using var escaneo = await cliente.PostAsJsonAsync(recepcion + "/bultos", new EscanearRecepcionCommand(bulto.Codigo, estado.Version));
                escaneo.EnsureSuccessStatusCode();
                estado = (await escaneo.Content.ReadFromJsonAsync<RegistroRecepcionDto>(JsonApi))!.Recepcion;
            }
            (await cliente.PostAsJsonAsync(recepcion + "/confirmar", new ConfirmarRecepcionCommand(estado.Version))).EnsureSuccessStatusCode();
        }
        var envios = (await cliente.GetFromJsonAsync<EnvioAsignableDto[]>("/api/despacho/envios/disponibles", JsonApi))!;
        Assert.Equal(2, envios.Length);
        var ruta = Assert.Single((await cliente.GetFromJsonAsync<RutaAsignacionDto[]>("/api/despacho/rutas/planificables", JsonApi))!);
        var primero = SolicitudAsignacion(envios.Single(x => x.Id == e.Envio.Id), ruta, 7);
        var url = $"/api/despacho/rutas/{ruta.Id}/envios";
        using var respuesta = await cliente.PostAsJsonAsync(url, primero);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var asignado = (await respuesta.Content.ReadFromJsonAsync<EnvioAsignadoDto>(JsonApi))!;
        Assert.Equal(e.Envio.Bultos.Single().Id, Assert.Single(asignado.Bultos));
        Assert.Equal((2.1234567m, 0.006m), (asignado.PesoKg, asignado.VolumenM3));
        Assert.NotEqual(primero.VersionEsperadaEnvio, asignado.VersionEnvio);
        Assert.NotEqual(rutaCreada.Version, asignado.VersionRuta);
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PostAsJsonAsync(url, primero)).StatusCode);
        ruta = Assert.Single((await cliente.GetFromJsonAsync<RutaAsignacionDto[]>("/api/despacho/rutas/planificables", JsonApi))!);
        Assert.Equal((1, 8, 2.1234567m), (ruta.ParadasActivas, ruta.ProximoOrden, ruta.PesoOcupadoKg));
        var segundo = SolicitudAsignacion(envios.Single(x => x.Id == segundoId), ruta, 2);
        Assert.Equal(HttpStatusCode.Created, (await cliente.PostAsJsonAsync(url, segundo)).StatusCode);
        Assert.Empty((await cliente.GetFromJsonAsync<EnvioAsignableDto[]>("/api/despacho/envios/disponibles", JsonApi))!);

        var cargaUrl = $"/api/carga/rutas/{ruta.Id}";
        var carga = (await cliente.GetFromJsonAsync<EstadoCargaRutaDto>(cargaUrl, JsonApi))!;
        foreach (var bulto in carga.Bultos)
        {
            using var escaneo = await cliente.PostAsJsonAsync(cargaUrl + "/bultos", new RegistrarCargaRutaCommand(bulto.Codigo, carga.Version, bulto.BultoId));
            escaneo.EnsureSuccessStatusCode();
            carga = (await cliente.GetFromJsonAsync<EstadoCargaRutaDto>(cargaUrl, JsonApi))!;
        }
        (await cliente.PostAsJsonAsync($"/api/despacho/rutas/{ruta.Id}/despachar", new DespacharRutaCommand(carga.Version))).EnsureSuccessStatusCode();
        var hojas = new HojasRuta(Options(), new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Repartidor.Id, RolUsuario.Repartidor)));
        var hoja = (await hojas.ObtenerAsync(ruta.Id))!;
        Assert.Equal(new[] { 2, 7 }, hoja.Paradas.Select(p => p.Orden));
        Assert.Empty((await cliente.GetFromJsonAsync<RutaAsignacionDto[]>("/api/despacho/rutas/planificables", JsonApi))!);
        await using var dbFinal = Context();
        var envio = await EscenarioPersistencia.Grafo(dbFinal).SingleAsync(x => x.Id == e.Envio.Id);
        Assert.Equal(e.Envio.VentanaDesde, envio.VentanaDesde);
        Assert.Equal(e.Envio.TarifaCalculada, envio.TarifaCalculada);
        Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
        Assert.Single(await dbFinal.Set<MensajeOutbox>().Where(m => m.Evento.Envio.Id == e.Envio.Id && m.Evento.Tipo == TipoEvento.Asignar).ToListAsync());
    }

    [PostgresFact]
    public async Task ApiAsignacionRegularRechazaRolesAjenosCsrfYVersionesViejas()
    {
        var (e, ruta, _) = await PrepararAsignacionRegular();
        var ajeno = new EscenarioPersistencia();
        await using (var db = Context(ajeno.Operador.Id)) { db.Add(ajeno.Admin); await db.SaveChangesAsync(); }
        Usuario deposito;
        await using (var db = Context())
        {
            deposito = new Usuario(await db.Set<Operador>().SingleAsync(), "deposito@example.com", RolUsuario.OperarioDeposito);
            db.Add(deposito); await db.SaveChangesAsync();
        }
        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin);
        using var anonimo = servidor.Cliente();
        using var repartidor = servidor.Cliente(e);
        using var operario = servidor.Cliente(e, deposito);
        using var otro = servidor.Cliente(ajeno, ajeno.Admin);
        var disponibles = "/api/despacho/envios/disponibles";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(disponibles)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(disponibles)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operario.GetAsync(disponibles)).StatusCode);
        Assert.Empty((await otro.GetFromJsonAsync<EnvioAsignableDto[]>(disponibles, JsonApi))!);
        Assert.Empty((await otro.GetFromJsonAsync<RutaAsignacionDto[]>("/api/despacho/rutas/planificables", JsonApi))!);
        var recursosAjenos = (await otro.GetFromJsonAsync<RecursosAsignacionDto>("/api/despacho/recursos", JsonApi))!;
        Assert.Equal(ajeno.Repartidor.Id, Assert.Single(recursosAjenos.Repartidores).Id);
        Assert.Equal(ajeno.Vehiculo.Id, Assert.Single(recursosAjenos.Vehiculos).Id);
        var envio = (await HandlerAsignacion(e).ListarEnviosAsync()).Single(x => x.Id == e.Envio.Id);
        var solicitud = SolicitudAsignacion(envio, Assert.Single(await HandlerAsignacion(e).ListarRutasAsync()));
        var url = $"/api/despacho/rutas/{ruta.Id}/envios";
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url, solicitud)).StatusCode);
        await Antiforgery(admin); await Antiforgery(otro);
        Assert.Equal(HttpStatusCode.Forbidden, (await otro.PostAsJsonAsync(url, solicitud)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync(url, solicitud with { EnvioId = ajeno.Envio.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/despacho/rutas/{Guid.NewGuid()}/envios", solicitud)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url, solicitud with { Orden = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url, solicitud with { VersionEsperadaEnvio = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url, solicitud with { VersionEsperadaRuta = Guid.NewGuid() })).StatusCode);
        Assert.Equal(envio.Version, (await HandlerAsignacion(e).ListarEnviosAsync()).Single(x => x.Id == envio.Id).Version);
        Assert.Equal(ruta.Version, Assert.Single(await HandlerAsignacion(e).ListarRutasAsync()).Version);
        await using var lectura = Context();
        Assert.Empty(await lectura.Set<Parada>().ToListAsync());
    }

    [PostgresFact]
    public async Task AsignacionRegularRevalidaIdentidadYNoAceptaProcesosOCuentasComerciales()
    {
        var (e, _, _) = await PrepararAsignacionRegular();
        var comercio = e.Envio.CuentaComercial.Usuarios.Single();
        foreach (var tenant in new[] { TenantActual.ParaOperador(e.Operador.Id),
            new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, comercio.Id, RolUsuario.Comercio, e.Envio.CuentaComercial.Id)),
            new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Repartidor.Id, RolUsuario.Administrador)) })
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => new AsignacionEnvioRutaRepository(Options(), tenant).ListarEnviosAsync(default));
        await using (var db = Context())
        { (await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id)).Desactivar(); await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => HandlerAsignacion(e).ListarRutasAsync());
    }

    [PostgresFact]
    public async Task AsignacionRegularNoAceptaParcialesNiCambiaSuComposicion()
    {
        var (e, _) = await RegistrarPrimeraParcial();
        var gestion = new DespachoPendientes(Options(), TenantAsignacion(e), RelojAsignacion());
        var ruta = await gestion.CrearRutaAsync(new(e.Repartidor.Id, e.Vehiculo.Id, 10), default);
        var parcial = await gestion.ConsultarAsync(e.Envio.Id, default);
        Assert.Empty(await HandlerAsignacion(e).ListarEnviosAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => HandlerAsignacion(e).Handle(ruta.Id,
            new(e.Envio.Id, parcial.VersionEnvio, ruta.Version, 1, new DateTimeOffset(EscenarioPersistencia.Fecha.AddDays(1)))));
        await using var db = Context();
        Assert.Equal(2, await db.Set<Bulto>().CountAsync(b => b.Estado == EstadoBulto.Entregado));
        Assert.Empty(await db.Set<Parada>().Where(p => p.Ruta.Id == ruta.Id).ToListAsync());
    }

    [PostgresFact]
    public async Task AsignacionRegularRespetaCapacidadVentanaYOrdenSinGuardarRechazos()
    {
        var (e, _, segundoId) = await PrepararAsignacionRegular(capacidadPeso: 3, maxParadas: 1);
        var handler = HandlerAsignacion(e);
        var envios = await handler.ListarEnviosAsync();
        var ruta = Assert.Single(await handler.ListarRutasAsync());
        var solicitud = SolicitudAsignacion(envios.Single(x => x.Id == e.Envio.Id), ruta);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(ruta.Id,
            solicitud with { LlegadaEstimada = new DateTimeOffset(envios.First().VentanaHasta) }));
        await handler.Handle(ruta.Id, solicitud);
        ruta = Assert.Single(await handler.ListarRutasAsync());
        Assert.Equal((1, 1), (ruta.ParadasActivas, ruta.MaxParadas));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(ruta.Id,
            SolicitudAsignacion(envios.Single(x => x.Id == segundoId), ruta, 1))); // Orden duplicado.
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(ruta.Id,
            SolicitudAsignacion(envios.Single(x => x.Id == segundoId), ruta, 2))); // Máximo de paradas.
        Assert.Equal(ruta.Version, Assert.Single(await handler.ListarRutasAsync()).Version);
        await using var db = Context();
        Assert.Single(await db.Set<Parada>().ToListAsync());
        Assert.Equal(EstadoEnvio.EnDeposito, (await db.Envios.SingleAsync(x => x.Id == segundoId)).Estado);
    }

    [PostgresFact]
    public async Task AsignacionRegularReprogramadaConservaNuevaVentanaYParadaHistorica()
    {
        var (e, ruta, _) = await PrepararAsignacionRegular();
        var handler = HandlerAsignacion(e);
        var envio = (await handler.ListarEnviosAsync()).Single(x => x.Id == e.Envio.Id);
        await handler.Handle(ruta.Id, SolicitudAsignacion(envio, Assert.Single(await handler.ListarRutasAsync())));
        await using (var db = Context())
        {
            var actual = await EscenarioPersistencia.Grafo(db).SingleAsync(x => x.Id == e.Envio.Id);
            actual.Aplazar(ProgramacionEntrega.Crear(actual.Zona.Franjas.Single(), new(2026, 10, 15), TimeSpan.FromHours(-3)),
                await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id), EscenarioPersistencia.Fecha.AddMinutes(40));
            await db.SaveChangesAsync();
        }
        envio = (await handler.ListarEnviosAsync()).Single(x => x.Id == e.Envio.Id);
        Assert.Equal(EstadoEnvio.Reprogramado, envio.Estado);
        handler = new(RepositorioAsignacion(e), new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddMinutes(45))));
        var resultado = await handler.Handle(ruta.Id, SolicitudAsignacion(envio, Assert.Single(await handler.ListarRutasAsync())));
        Assert.Equal(2, resultado.Orden); // No reutiliza el código de la parada histórica.
        await using var lectura = Context();
        var final = await EscenarioPersistencia.Grafo(lectura).SingleAsync(x => x.Id == e.Envio.Id);
        Assert.Equal(envio.VentanaDesde, final.VentanaDesde);
        Assert.Equal(2, await lectura.Set<Parada>().CountAsync());
        Assert.Single(await lectura.Set<Parada>().Where(p => p.Activa).ToListAsync());
        Assert.Single(final.Eventos, v => v.Tipo == TipoEvento.Reasignar);
    }

    [PostgresFact]
    public async Task AsignacionRegularFalloDeOutboxRevierteAmbasVersionesYParada()
    {
        var (e, _, _) = await PrepararAsignacionRegular();
        var handler = HandlerAsignacion(e);
        var envio = (await handler.ListarEnviosAsync()).Single(x => x.Id == e.Envio.Id);
        var ruta = Assert.Single(await handler.ListarRutasAsync());
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" ADD CONSTRAINT \"FalloAsignacion\" CHECK (false) NOT VALID");
        var solicitud = SolicitudAsignacion(envio, ruta);
        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(ruta.Id, solicitud));
        Assert.Equal(envio.Version, (await handler.ListarEnviosAsync()).Single(x => x.Id == envio.Id).Version);
        Assert.Equal(ruta.Version, Assert.Single(await handler.ListarRutasAsync()).Version);
        await using (var db = Context())
        {
            Assert.Empty(await db.Set<Parada>().ToListAsync());
            Assert.Empty(await db.Set<EventoDeEnvio>().Where(v => v.Tipo == TipoEvento.Asignar).ToListAsync());
            Assert.Equal(EstadoEnvio.EnDeposito, (await db.Envios.SingleAsync(x => x.Id == envio.Id)).Estado);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" DROP CONSTRAINT \"FalloAsignacion\"");
        }
        await handler.Handle(ruta.Id, solicitud);
    }

    [PostgresFact]
    public async Task AsignacionRegularConcurrenteDelMismoEnvioConfirmaUnaSolaRuta()
    {
        var (e, rutaA, _) = await PrepararAsignacionRegular();
        var rutaB = await new DespachoPendientes(Options(), TenantAsignacion(e), RelojAsignacion())
            .CrearRutaAsync(new(e.Repartidor.Id, e.Vehiculo.Id, 10), default);
        var servicio = HandlerAsignacion(e);
        var envio = (await servicio.ListarEnviosAsync()).Single(x => x.Id == e.Envio.Id);
        var rutas = await servicio.ListarRutasAsync();
        var handler = new AsignarEnvioRutaHandler(new BarreraAsignacionRegular(RepositorioAsignacion(e)), RelojAsignacion());
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => handler.Handle(rutaA.Id, SolicitudAsignacion(envio, rutas.Single(r => r.Id == rutaA.Id)))),
            Record.ExceptionAsync(() => handler.Handle(rutaB.Id, SolicitudAsignacion(envio, rutas.Single(r => r.Id == rutaB.Id)))));
        Assert.Single(errores, x => x is null);
        Assert.IsType<ConflictoConcurrenciaException>(Assert.Single(errores, x => x is not null));
        await using var db = Context();
        Assert.Single(await db.Set<Parada>().Where(p => p.Activa).ToListAsync());
        Assert.Single(await db.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.Asignar).ToListAsync());
    }

    [PostgresFact]
    public Task AsignacionRegularConcurrenteDeDosEnviosNoSuperaCapacidadDeLaRuta() => VerificarCarreraCapacidadRegular(false);

    [PostgresFact]
    public Task AsignacionRegularConcurrenteConElMismoOrdenDevuelveConflictoSinDuplicar() => VerificarCarreraCapacidadRegular(true);

    private async Task VerificarCarreraCapacidadRegular(bool mismoOrden)
    {
        var (e, _, _) = await PrepararAsignacionRegular(capacidadPeso: 3);
        var servicio = HandlerAsignacion(e);
        var envios = await servicio.ListarEnviosAsync();
        var ruta = Assert.Single(await servicio.ListarRutasAsync());
        var handler = new AsignarEnvioRutaHandler(new BarreraAsignacionRegular(RepositorioAsignacion(e)), RelojAsignacion());
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => handler.Handle(ruta.Id, SolicitudAsignacion(envios[0], ruta, 1))),
            Record.ExceptionAsync(() => handler.Handle(ruta.Id, SolicitudAsignacion(envios[1], ruta, mismoOrden ? 1 : 2))));
        Assert.Single(errores, x => x is null);
        Assert.IsType<ConflictoConcurrenciaException>(Assert.Single(errores, x => x is not null));
        var restante = Assert.Single(await servicio.ListarEnviosAsync());
        var actual = Assert.Single(await servicio.ListarRutasAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => servicio.Handle(ruta.Id, SolicitudAsignacion(restante, actual)));
        Assert.True(actual.PesoOcupadoKg <= actual.CapacidadPesoKg);
        await using var db = Context();
        Assert.Single(await db.Set<Parada>().ToListAsync());
        Assert.Single(await db.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.Asignar).ToListAsync());
    }

    private sealed class BarreraAsignacionRegular(IAsignacionEnvioRutaRepository inner) : IAsignacionEnvioRutaRepository
    {
        private int _abiertas;
        private readonly TaskCompletionSource _listas = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<EnvioAsignableDto>> ListarEnviosAsync(CancellationToken ct) => inner.ListarEnviosAsync(ct);
        public Task<IReadOnlyList<RutaAsignacionDto>> ListarRutasAsync(CancellationToken ct) => inner.ListarRutasAsync(ct);
        public Task<RecursosAsignacionDto> RecursosAsync(CancellationToken ct) => inner.RecursosAsync(ct);
        public async Task<IOperacionAsignacionEnvioRuta> AbrirAsync(Guid envioId, Guid rutaId, CancellationToken ct)
        {
            var op = await inner.AbrirAsync(envioId, rutaId, ct);
            if (Interlocked.Increment(ref _abiertas) == 2) _listas.TrySetResult();
            try { await _listas.Task.WaitAsync(TimeSpan.FromSeconds(20), ct); return op; }
            catch { await op.DisposeAsync(); throw; }
        }
    }
}
