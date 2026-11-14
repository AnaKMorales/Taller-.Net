using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Deposito;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Deposito;
using UltimaMilla.Infrastructure.Persistence.Despacho;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private async Task<(EscenarioPersistencia E, Guid RutaId)> PrepararCierreDeposito(bool reintentable = false,
        bool intento = true, bool despachada = true, bool varios = false)
    {
        var e = Escenario(reintentable, parcial: varios);
        await using var db = Context();
        db.AddRange(e.Envio, e.Repartidor, e.Vehiculo);
        await db.SaveChangesAsync();
        e.Recibir(); await db.SaveChangesAsync();
        var ruta = e.Asignar(); db.Add(ruta); await db.SaveChangesAsync();
        if (despachada)
        {
            foreach (var b in e.Envio.Bultos)
                db.Add(EscaneoBulto.RegistrarCarga(ruta, b, e.Admin, EscenarioPersistencia.Fecha));
            ruta.Despachar(e.Admin, EscenarioPersistencia.Fecha);
            await db.SaveChangesAsync();
            if (intento)
            {
                e.Envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reglas.MotivosNoEntrega.Single(), [e.Foto()], e.Repartidor,
                    EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid(), new GeoPunto(-34, -56));
                await db.SaveChangesAsync();
            }
        }
        return (e, ruta.Id);
    }
    private CierreDepositoRepository RepositorioCierre(EscenarioPersistencia e) => new(Options(),
        new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador)));
    private static RelojFijo RelojCierre() => new(new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(3)));
    private DevolucionDepositoHandler HandlerDevolucion(EscenarioPersistencia e) => new(RepositorioCierre(e), RelojCierre());
    private RendirRutaHandler HandlerRendicion(EscenarioPersistencia e) => new(RepositorioCierre(e), RelojCierre());
    private async Task<EstadoDevolucionEnvioDto> AbrirYEscanearDevolucion(EscenarioPersistencia e, bool escanear = true)
    {
        var handler = HandlerDevolucion(e);
        var antes = await handler.ConsultarAsync(e.Envio.Id);
        var estado = await handler.IniciarAsync(e.Envio.Id, new(antes.Version));
        if (escanear)
            foreach (var bulto in estado.Devolucion!.Bultos)
                estado = (await handler.EscanearAsync(e.Envio.Id, new(bulto.Codigo, estado.Version))).Envio;
        return estado;
    }

    [PostgresFact]
    public async Task ApiCierreDepositoIntegraDevolucionCompletaYRendicionConPermisosYCsrf()
    {
        var (e, rutaId) = await PrepararCierreDeposito();
        Usuario deposito;
        await using (var db = Context())
        {
            deposito = new Usuario(await db.Set<Operador>().SingleAsync(), "deposito@example.com", RolUsuario.OperarioDeposito);
            db.Add(deposito); await db.SaveChangesAsync();
        }
        var ajeno = new EscenarioPersistencia();
        await using (var db = Context(ajeno.Operador.Id)) { db.Add(ajeno.Admin); await db.SaveChangesAsync(); }
        using var servidor = new ServidorApi(_testConnection);
        using var operario = servidor.Cliente(e, deposito);
        using var anonimo = servidor.Cliente();
        using var repartidor = servidor.Cliente(e);
        using var otro = servidor.Cliente(ajeno, ajeno.Admin);
        ((RelojConfiguracion)servidor.Services.GetRequiredService<TimeProvider>()).Ahora = EscenarioPersistencia.Fecha.AddHours(3);
        var url = $"/api/deposito/envios/{e.Envio.Id}/devolucion";
        var rutaUrl = $"/api/deposito/rutas/{rutaId}";
        foreach (var consulta in new[] { url, rutaUrl + "/rendicion" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(consulta)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(consulta)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await otro.GetAsync(consulta)).StatusCode);
        }
        Assert.Empty((await otro.GetFromJsonAsync<DevolucionAbiertaDto[]>("/api/deposito/devoluciones/abiertas", JsonApi))!);
        Assert.Empty((await otro.GetFromJsonAsync<RutaPorRendirDto[]>("/api/deposito/rutas/por-rendir", JsonApi))!);
        var estado = (await operario.GetFromJsonAsync<EstadoDevolucionEnvioDto>(url, JsonApi))!;
        var ruta = (await operario.GetFromJsonAsync<EstadoRendicionRutaDto>(rutaUrl + "/rendicion", JsonApi))!;
        Assert.Equal(0, ruta.ParadasPendientes);
        Assert.Null(estado.Devolucion);
        Assert.Equal(HttpStatusCode.BadRequest, (await operario.PostAsJsonAsync(url + "/iniciar", new VersionDevolucionCommand(estado.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await operario.PostAsJsonAsync(rutaUrl + "/rendir", new RendirRutaCommand(ruta.Version))).StatusCode);
        await Antiforgery(operario); await Antiforgery(otro);
        Assert.Equal(HttpStatusCode.Forbidden, (await otro.PostAsJsonAsync(url + "/iniciar", new VersionDevolucionCommand(estado.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otro.PostAsJsonAsync(rutaUrl + "/rendir", new RendirRutaCommand(ruta.Version))).StatusCode);
        using var inicio = await operario.PostAsJsonAsync(url + "/iniciar", new VersionDevolucionCommand(estado.Version));
        Assert.Equal(HttpStatusCode.Created, inicio.StatusCode);
        var versionAnterior = estado.Version;
        estado = (await inicio.Content.ReadFromJsonAsync<EstadoDevolucionEnvioDto>(JsonApi))!;
        Assert.NotEqual(versionAnterior, estado.Version);
        Assert.Single((await operario.GetFromJsonAsync<DevolucionAbiertaDto[]>("/api/deposito/devoluciones/abiertas", JsonApi))!);
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(url + "/completar", new VersionDevolucionCommand(estado.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await operario.PostAsJsonAsync(url + "/bultos", new EscanearDevolucionCommand("AJENO", estado.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(url + "/bultos", new EscanearDevolucionCommand("B001", versionAnterior))).StatusCode);
        var solicitud = new EscanearDevolucionCommand(" b001 ", estado.Version);
        using var escaneo = await operario.PostAsJsonAsync(url + "/bultos", solicitud);
        Assert.Equal(HttpStatusCode.OK, escaneo.StatusCode);
        estado = (await escaneo.Content.ReadFromJsonAsync<EscaneoDevolucionDto>(JsonApi))!.Envio;
        using var repetido = await operario.PostAsJsonAsync(url + "/bultos", solicitud);
        Assert.True((await repetido.Content.ReadFromJsonAsync<EscaneoDevolucionDto>(JsonApi))!.Repetido);
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(url + "/completar", new VersionDevolucionCommand(solicitud.VersionEsperada))).StatusCode);
        using var cierre = await operario.PostAsJsonAsync(url + "/completar", new VersionDevolucionCommand(estado.Version));
        Assert.Equal(HttpStatusCode.OK, cierre.StatusCode);
        estado = (await cierre.Content.ReadFromJsonAsync<EstadoDevolucionEnvioDto>(JsonApi))!;
        Assert.Equal(EstadoEnvio.Devuelto, estado.Estado);
        Assert.Equal(EstadoDevolucion.Completado, estado.Devolucion!.Estado);
        Assert.Equal(0, estado.Devolucion.Faltantes);
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(url + "/completar", new VersionDevolucionCommand(estado.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(url + "/bultos", solicitud)).StatusCode);
        Assert.Empty((await operario.GetFromJsonAsync<DevolucionAbiertaDto[]>("/api/deposito/devoluciones/abiertas", JsonApi))!);
        Assert.Single((await operario.GetFromJsonAsync<RutaPorRendirDto[]>("/api/deposito/rutas/por-rendir", JsonApi))!);
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(rutaUrl + "/rendir", new RendirRutaCommand(Guid.NewGuid()))).StatusCode);
        using var rendicion = await operario.PostAsJsonAsync(rutaUrl + "/rendir", new RendirRutaCommand(ruta.Version));
        Assert.Equal(HttpStatusCode.OK, rendicion.StatusCode);
        var final = (await rendicion.Content.ReadFromJsonAsync<EstadoRendicionRutaDto>(JsonApi))!;
        Assert.NotNull(final.RendicionId);
        Assert.NotEqual(ruta.Version, final.Version);
        Assert.Equal(EstadoEnvio.Devuelto, Assert.Single(final.Paradas).EstadoEnvio);
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(rutaUrl + "/rendir", new RendirRutaCommand(final.Version))).StatusCode);
        Assert.Empty((await operario.GetFromJsonAsync<RutaPorRendirDto[]>("/api/deposito/rutas/por-rendir", JsonApi))!);
        var hojas = new HojasRuta(Options(), new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Repartidor.Id, RolUsuario.Repartidor)));
        Assert.Null(await hojas.ObtenerAsync(rutaId));
        await using var lectura = Context();
        Assert.Single(await lectura.Set<Rendicion>().ToListAsync());
        Assert.Equal(deposito.Id, (await lectura.Set<EscaneoBulto>().Include(s => s.Responsable)
            .SingleAsync(x => x.Tipo == TipoEscaneo.Devolucion)).Responsable.Id);
        Assert.Single(await lectura.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.DevolucionExitosa && m.Evento.Responsable!.Id == deposito.Id).ToListAsync());
        Assert.Equal(7, await lectura.Set<MensajeOutbox>().CountAsync()); // Rendir no crea un evento de envío nuevo.
    }

    [PostgresFact]
    public async Task CierreDepositoParcialSoloRecibePendientesYConservaAvisosV2()
    {
        var e = await PrepararVisitaParcial(true, AccionBultosPendientes.DevolverAlComercio);
        var comando = await ComandoRegistro(e);
        await RegistroHandler(await IdentidadRepartidor(e)).Handle(comando);
        var handler = HandlerDevolucion(e);
        var estado = await AbrirYEscanearDevolucion(e, escanear: false);
        var pendiente = Assert.Single(estado.Devolucion!.Bultos);
        var entregado = e.Envio.Bultos.First(b => b.Id != pendiente.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.EscanearAsync(e.Envio.Id, new(entregado.Codigo, estado.Version)));
        estado = (await handler.EscanearAsync(e.Envio.Id, new(pendiente.Codigo, estado.Version))).Envio;
        estado = await handler.CompletarAsync(e.Envio.Id, new(estado.Version));
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, estado.Estado);
        Assert.Equal(EstadoBulto.Devuelto, Assert.Single(estado.Devolucion!.Bultos).Estado);
        await using var lectura = Context();
        Assert.Equal(2, await lectura.Set<Bulto>().CountAsync(b => b.Estado == EstadoBulto.Entregado));
        Assert.Single(await lectura.Set<EscaneoBulto>().Where(s => s.Tipo == TipoEscaneo.Devolucion).ToListAsync());
        var mensaje = Assert.Single(await lectura.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.DevolucionExitosa).ToListAsync());
        Assert.Equal("envio.evento.v2", mensaje.Tipo);
    }

    [PostgresFact]
    public async Task CierreDepositoRespetaPoliticaDeReintentoYParadasActivas()
    {
        var (e, rutaId) = await PrepararCierreDeposito(reintentable: true, intento: false);
        var handler = HandlerRendicion(e);
        var ruta = await handler.ConsultarAsync(rutaId);
        Assert.Equal(1, ruta.ParadasPendientes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(rutaId, new(ruta.Version)));
        await using (var db = Context())
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            envio.RegistrarIntento(ResultadoIntento.Fallido, envio.Reglas.MotivosNoEntrega.Single(), [e.Foto()],
                await db.Set<Repartidor>().SingleAsync(), EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid());
            await db.SaveChangesAsync();
        }
        var dev = HandlerDevolucion(e);
        var antes = await dev.ConsultarAsync(e.Envio.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => dev.IniciarAsync(e.Envio.Id, new(antes.Version)));
        Assert.Equal(antes.Version, (await dev.ConsultarAsync(e.Envio.Id)).Version);
        await using var lectura = Context();
        Assert.Empty(await lectura.Set<Devolucion>().ToListAsync());
        Assert.Empty(await lectura.Set<Rendicion>().ToListAsync());
    }

    [PostgresFact]
    public async Task CierreDepositoRendirNoCompletaDevolucionesNiImpideCerrarlasDespues()
    {
        var (e, rutaId) = await PrepararCierreDeposito();
        var estado = await AbrirYEscanearDevolucion(e, escanear: false);
        var rendir = HandlerRendicion(e);
        await rendir.Handle(rutaId, new((await rendir.ConsultarAsync(rutaId)).Version));
        Assert.Equal(estado.Version, (await HandlerDevolucion(e).ConsultarAsync(e.Envio.Id)).Version);
        var escaneo = await HandlerDevolucion(e).EscanearAsync(e.Envio.Id, new("B001", estado.Version));
        var final = await HandlerDevolucion(e).CompletarAsync(e.Envio.Id, new(escaneo.Envio.Version));
        Assert.Equal(EstadoEnvio.Devuelto, final.Estado);
        Assert.NotNull((await rendir.ConsultarAsync(rutaId)).RendicionId);
    }

    [PostgresFact]
    public async Task CierreDepositoRevalidaIdentidadYRechazaContextosNoAutorizados()
    {
        var (e, _) = await PrepararCierreDeposito();
        var comercio = e.Envio.CuentaComercial.Usuarios.Single();
        foreach (var tenant in new[] { TenantActual.ParaOperador(e.Operador.Id),
            new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, comercio.Id, RolUsuario.Comercio, e.Envio.CuentaComercial.Id)),
            new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Repartidor.Id, RolUsuario.Administrador)) })
        {
            var repo = new CierreDepositoRepository(Options(), tenant);
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => repo.ListarAbiertasAsync(default));
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => repo.ListarAsync(default));
        }
        await using (var db = Context())
        { (await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id)).Desactivar(); await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => HandlerDevolucion(e).ConsultarAsync(e.Envio.Id));
    }

    [PostgresFact]
    public async Task CierreDepositoIniciosConcurrentesGeneranUnaSolaDevolucion()
    {
        var (e, _) = await PrepararCierreDeposito();
        var version = (await HandlerDevolucion(e).ConsultarAsync(e.Envio.Id)).Version;
        var handler = new DevolucionDepositoHandler(new BarreraCierreDeposito(RepositorioCierre(e)), RelojCierre());
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => handler.IniciarAsync(e.Envio.Id, new(version))),
            Record.ExceptionAsync(() => handler.IniciarAsync(e.Envio.Id, new(version))));
        ComprobarUnCierre(errores);
        await using var lectura = Context();
        Assert.Single(await lectura.Set<Devolucion>().ToListAsync());
        Assert.Single(await lectura.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.Devolver).ToListAsync());
    }

    [PostgresFact]
    public async Task CierreDepositoEscaneosConcurrentesNoSeSobrescribenYReconocenReenvio()
    {
        var (e, _) = await PrepararCierreDeposito(varios: true);
        var inicio = await AbrirYEscanearDevolucion(e, escanear: false);
        var handler = new DevolucionDepositoHandler(new BarreraCierreDeposito(RepositorioCierre(e)), RelojCierre());
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => handler.EscanearAsync(e.Envio.Id, new("B001", inicio.Version))),
            Record.ExceptionAsync(() => handler.EscanearAsync(e.Envio.Id, new("B002", inicio.Version))));
        ComprobarUnCierre(errores);
        var estado = await HandlerDevolucion(e).ConsultarAsync(e.Envio.Id);
        var recibido = Assert.Single(estado.Devolucion!.Bultos, b => b.Recibido);
        Assert.True((await HandlerDevolucion(e).EscanearAsync(e.Envio.Id, new(recibido.Codigo, inicio.Version))).Repetido);
        await using var lectura = Context();
        Assert.Single(await lectura.Set<EscaneoBulto>().Where(s => s.Tipo == TipoEscaneo.Devolucion).ToListAsync());
    }

    [PostgresFact]
    public async Task CierreDepositoCompletarConcurrentementeGeneraUnSoloEventoYOutbox()
    {
        var (e, _) = await PrepararCierreDeposito();
        var antes = await AbrirYEscanearDevolucion(e);
        var handler = new DevolucionDepositoHandler(new BarreraCierreDeposito(RepositorioCierre(e)), RelojCierre());
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => handler.CompletarAsync(e.Envio.Id, new(antes.Version))),
            Record.ExceptionAsync(() => handler.CompletarAsync(e.Envio.Id, new(antes.Version))));
        ComprobarUnCierre(errores);
        await using var lectura = Context();
        Assert.Single(await lectura.Set<EventoDeEnvio>().Where(v => v.Tipo == TipoEvento.DevolucionExitosa).ToListAsync());
        Assert.Single(await lectura.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.DevolucionExitosa).ToListAsync());
    }

    [PostgresFact]
    public async Task CierreDepositoRendicionesConcurrentesGeneranUnSoloRegistro()
    {
        var (e, rutaId) = await PrepararCierreDeposito();
        var version = (await HandlerRendicion(e).ConsultarAsync(rutaId)).Version;
        var handler = new RendirRutaHandler(new BarreraCierreDeposito(RepositorioCierre(e)), RelojCierre());
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => handler.Handle(rutaId, new(version))),
            Record.ExceptionAsync(() => handler.Handle(rutaId, new(version))));
        ComprobarUnCierre(errores);
        await using var lectura = Context();
        Assert.Single(await lectura.Set<Rendicion>().ToListAsync());
    }

    [PostgresFact]
    public Task CierreDepositoFalloAlIniciarRevierteDevolucionEstadoEventoYVersion() => VerificarRollbackCierreDeposito("inicio");
    [PostgresFact]
    public Task CierreDepositoFalloAlEscanearRevierteEscaneoYVersion() => VerificarRollbackCierreDeposito("escaneo");
    [PostgresFact]
    public Task CierreDepositoFalloDeOutboxRevierteFechaEstadoEventoYVersion() => VerificarRollbackCierreDeposito("completar");
    [PostgresFact]
    public Task CierreDepositoFalloAlRendirRevierteRegistroYVersion() => VerificarRollbackCierreDeposito("rendir");

    private async Task VerificarRollbackCierreDeposito(string operacion)
    {
        var (e, rutaId) = await PrepararCierreDeposito();
        var handler = HandlerDevolucion(e);
        if (operacion is "escaneo" or "completar")
            await AbrirYEscanearDevolucion(e, escanear: operacion == "completar");
        var antes = await handler.ConsultarAsync(e.Envio.Id);
        var rutaAntes = await HandlerRendicion(e).ConsultarAsync(rutaId);
        var (agregar, quitar) = operacion switch {
            "inicio" => ("ALTER TABLE \"Devoluciones\" ADD CONSTRAINT \"FalloCierre\" CHECK (false) NOT VALID", "ALTER TABLE \"Devoluciones\" DROP CONSTRAINT \"FalloCierre\""),
            "escaneo" => ("ALTER TABLE \"EscaneosBulto\" ADD CONSTRAINT \"FalloCierre\" CHECK (false) NOT VALID", "ALTER TABLE \"EscaneosBulto\" DROP CONSTRAINT \"FalloCierre\""),
            "rendir" => ("ALTER TABLE \"Rendiciones\" ADD CONSTRAINT \"FalloCierre\" CHECK (false) NOT VALID", "ALTER TABLE \"Rendiciones\" DROP CONSTRAINT \"FalloCierre\""),
            _ => ("ALTER TABLE \"MensajesOutbox\" ADD CONSTRAINT \"FalloCierre\" CHECK (false) NOT VALID", "ALTER TABLE \"MensajesOutbox\" DROP CONSTRAINT \"FalloCierre\"") };
        int eventos, mensajes;
        await using (var db = Context())
        {
            eventos = await db.Set<EventoDeEnvio>().CountAsync(); mensajes = await db.Set<MensajeOutbox>().CountAsync();
            await db.Database.ExecuteSqlRawAsync(agregar);
        }
        Task Ejecutar() => operacion switch {
            "inicio" => handler.IniciarAsync(e.Envio.Id, new(antes.Version)),
            "escaneo" => handler.EscanearAsync(e.Envio.Id, new("B001", antes.Version)),
            "completar" => handler.CompletarAsync(e.Envio.Id, new(antes.Version)),
            _ => HandlerRendicion(e).Handle(rutaId, new(rutaAntes.Version)) };
        await Assert.ThrowsAsync<DbUpdateException>(Ejecutar);
        var despues = await handler.ConsultarAsync(e.Envio.Id);
        var rutaDespues = await HandlerRendicion(e).ConsultarAsync(rutaId);
        Assert.Equal((antes.Version, antes.Estado), (despues.Version, despues.Estado));
        Assert.Equal(antes.Devolucion?.FechaCierre, despues.Devolucion?.FechaCierre);
        Assert.Equal(antes.Devolucion?.Recibidos, despues.Devolucion?.Recibidos);
        Assert.Equal(rutaAntes.Version, rutaDespues.Version);
        Assert.Null(rutaDespues.RendicionId);
        await using (var db = Context())
        {
            Assert.Equal(eventos, await db.Set<EventoDeEnvio>().CountAsync());
            Assert.Equal(mensajes, await db.Set<MensajeOutbox>().CountAsync());
            await db.Database.ExecuteSqlRawAsync(quitar);
        }
        await Ejecutar();
    }

    private static void ComprobarUnCierre(Exception?[] errores)
    {
        Assert.Single(errores, e => e is null);
        var error = Assert.Single(errores, e => e is not null);
        Assert.True(error is ConflictoConcurrenciaException, error?.ToString());
    }
    private sealed class BarreraCierreDeposito(CierreDepositoRepository inner) : IDevolucionDepositoRepository, IRendicionRutaRepository
    {
        private int _abiertas;
        private readonly TaskCompletionSource _listas = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<DevolucionAbiertaDto>> ListarAbiertasAsync(CancellationToken ct) => inner.ListarAbiertasAsync(ct);
        public Task<IReadOnlyList<RutaPorRendirDto>> ListarAsync(CancellationToken ct) => inner.ListarAsync(ct);
        public async Task<IOperacionDevolucionDeposito> AbrirAsync(Guid id, CancellationToken ct) => await Esperar(await inner.AbrirAsync(id, ct), ct);
        public async Task<IOperacionRendicionRuta> AbrirRutaAsync(Guid id, CancellationToken ct) => await Esperar(await inner.AbrirRutaAsync(id, ct), ct);
        private async Task<T> Esperar<T>(T op, CancellationToken ct) where T : IAsyncDisposable
        {
            if (Interlocked.Increment(ref _abiertas) == 2) _listas.TrySetResult();
            try { await _listas.Task.WaitAsync(TimeSpan.FromSeconds(20), ct); return op; }
            catch { await op.DisposeAsync(); throw; }
        }
    }
}
