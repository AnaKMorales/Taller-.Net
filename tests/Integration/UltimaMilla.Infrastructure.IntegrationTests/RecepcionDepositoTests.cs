using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Deposito;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Deposito;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private async Task<EscenarioPersistencia> PrepararRecepcion(bool varios = false)
    {
        var e = Escenario(parcial: varios);
        await using var db = Context();
        db.Add(e.Envio);
        await db.SaveChangesAsync();
        return e;
    }

    private RecepcionDepositoRepository RepositorioRecepcion(EscenarioPersistencia e, Usuario? actor = null) =>
        new(Options(), new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, (actor ?? e.Admin).Id, (actor ?? e.Admin).Rol)));
    private RecepcionDepositoHandler HandlerRecepcion(EscenarioPersistencia e) => new(RepositorioRecepcion(e),
        new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(1))));

    [PostgresFact]
    public async Task ApiRecepcionValidaPermisosVersionYCsrfYConservaDeclaracionConDiscrepancias()
    {
        var e = await PrepararRecepcion(varios: true);
        Usuario deposito;
        await using (var db = Context())
        {
            deposito = new Usuario(await db.Set<Operador>().SingleAsync(), "deposito@example.com", RolUsuario.OperarioDeposito);
            db.Add(deposito); await db.SaveChangesAsync();
        }
        var otro = new EscenarioPersistencia();
        await using (var db = Context(otro.Operador.Id)) { db.Add(otro.Admin); await db.SaveChangesAsync(); }
        using var servidor = new ServidorApi(_testConnection);
        using var operario = servidor.Cliente(e, deposito);
        using var repartidor = servidor.Cliente(e);
        using var anonimo = servidor.Cliente();
        using var ajeno = servidor.Cliente(otro, otro.Admin);
        ((RelojConfiguracion)servidor.Services.GetRequiredService<TimeProvider>()).Ahora = EscenarioPersistencia.Fecha.AddHours(1);
        var url = $"/api/deposito/envios/{e.Envio.Id}/recepcion";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ajeno.GetAsync(url)).StatusCode);
        var estado = (await operario.GetFromJsonAsync<RecepcionDepositoDto>(url, JsonApi))!;
        Assert.Equal((3, 0, 3), (estado.Esperados, estado.Recibidos, estado.Faltantes));
        var escaneo = new EscanearRecepcionCommand(" b001 ", estado.Version);
        Assert.Equal(HttpStatusCode.BadRequest, (await operario.PostAsJsonAsync(url + "/bultos", escaneo)).StatusCode);
        await Antiforgery(operario);
        Assert.Equal(HttpStatusCode.BadRequest, (await operario.PostAsJsonAsync(url + "/bultos", escaneo with { Codigo = "AJENO" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(url + "/bultos", escaneo with { VersionEsperada = Guid.NewGuid() })).StatusCode);
        using var primero = await operario.PostAsJsonAsync(url + "/bultos", escaneo);
        Assert.Equal(HttpStatusCode.OK, primero.StatusCode);
        estado = (await primero.Content.ReadFromJsonAsync<RegistroRecepcionDto>(JsonApi))!.Recepcion;
        using var repeticion = await operario.PostAsJsonAsync(url + "/bultos", escaneo);
        Assert.True((await repeticion.Content.ReadFromJsonAsync<RegistroRecepcionDto>(JsonApi))!.Repetido);
        var diferencia = new RegistrarDiscrepanciaCommand(estado.Version, TipoDiscrepancia.PesoNoCoincide,
            "Peso medido en depósito", "2.1234567 kg", "3.1 kg", e.Envio.Bultos.First().Id);
        using var registro = await operario.PostAsJsonAsync(url + "/discrepancias", diferencia);
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);
        estado = (await registro.Content.ReadFromJsonAsync<RegistroRecepcionDto>(JsonApi))!.Recepcion;
        using var repetida = await operario.PostAsJsonAsync(url + "/discrepancias", diferencia);
        Assert.True((await repetida.Content.ReadFromJsonAsync<RegistroRecepcionDto>(JsonApi))!.Repetido);
        foreach (var bulto in estado.Bultos.Where(b => !b.Recibido))
        {
            using var respuesta = await operario.PostAsJsonAsync(url + "/bultos", new EscanearRecepcionCommand(bulto.Codigo, estado.Version));
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            estado = (await respuesta.Content.ReadFromJsonAsync<RegistroRecepcionDto>(JsonApi))!.Recepcion;
        }
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(url + "/confirmar", new ConfirmarRecepcionCommand(escaneo.VersionEsperada))).StatusCode);
        using var confirmacion = await operario.PostAsJsonAsync(url + "/confirmar", new ConfirmarRecepcionCommand(estado.Version));
        Assert.Equal(HttpStatusCode.OK, confirmacion.StatusCode);
        var final = (await confirmacion.Content.ReadFromJsonAsync<RecepcionDepositoDto>(JsonApi))!;
        Assert.Equal(EstadoEnvio.EnDeposito, final.Estado);
        Assert.Equal(0, final.Faltantes);
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(url + "/bultos", escaneo)).StatusCode);
        await using var lectura = Context();
        var envio = await EscenarioPersistencia.Grafo(lectura).SingleAsync();
        Assert.Equal(150.25m, envio.TarifaCalculada);
        Assert.Equal(2.1234567m, envio.Bultos.Single(b => b.Codigo == "B001").Peso);
        Assert.Equal(("2.1234567 kg", "3.1 kg"), (Assert.Single(envio.Discrepancias).DetalleDeclarado, envio.Discrepancias.Single().DetalleRecibido));
        Assert.All(envio.Bultos, b => Assert.Equal(deposito.Id, Assert.Single(b.Escaneos).Responsable.Id));
        Assert.Single(envio.Eventos, v => v.Tipo == TipoEvento.Validar && v.ResponsableId == deposito.Id);
        Assert.Single(await lectura.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.Validar).ToListAsync());
    }

    [PostgresFact]
    public async Task RecepcionConFaltantesYSobrantesNoCompletaNiCambiaElManifiesto()
    {
        var e = await PrepararRecepcion();
        var handler = HandlerRecepcion(e);
        var estado = await handler.ConsultarAsync(e.Envio.Id);
        var faltante = await handler.RegistrarDiscrepanciaAsync(e.Envio.Id, new(estado.Version, TipoDiscrepancia.BultoFaltante,
            "No recibido", "B001", "Ausente", e.Envio.Bultos.Single().Id));
        var sobrante = await handler.RegistrarDiscrepanciaAsync(e.Envio.Id, new(faltante.Recepcion.Version,
            TipoDiscrepancia.BultoSobrante, "Código no declarado", "Ninguno", "EXTRA"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ConfirmarAsync(e.Envio.Id, new(sobrante.Recepcion.Version)));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.RegistrarDiscrepanciaAsync(e.Envio.Id,
            new(sobrante.Recepcion.Version, TipoDiscrepancia.Otro, "Ajeno", "A", "B", Guid.NewGuid())));
        await using var db = Context();
        Assert.Equal(EstadoEnvio.Admitido, (await db.Envios.SingleAsync()).Estado);
        Assert.Single(await db.Set<Bulto>().ToListAsync());
        Assert.Equal(2, await db.Set<Discrepancia>().CountAsync());
        Assert.Empty(await db.Set<EscaneoBulto>().ToListAsync());
        Assert.Equal(sobrante.Recepcion.Version, await db.Envios.Select(x => EF.Property<Guid>(x, "RowVersion")).SingleAsync());
        Assert.Empty(await db.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.Validar).ToListAsync());
    }

    [PostgresFact]
    public async Task RecepcionRevalidaUsuarioYRechazaCuentaComercialContextoInternoYRolesFalsos()
    {
        var e = await PrepararRecepcion();
        var comercio = e.Envio.CuentaComercial.Usuarios.Single();
        var cuenta = new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, comercio.Id, RolUsuario.Comercio, e.Envio.CuentaComercial.Id));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => new RecepcionDepositoRepository(Options(), cuenta).AbrirAsync(e.Envio.Id, default));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => new RecepcionDepositoRepository(Options(), TenantActual.ParaOperador(e.Operador.Id)).AbrirAsync(e.Envio.Id, default));
        var falsificado = new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Repartidor.Id, RolUsuario.Administrador));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => new RecepcionDepositoRepository(Options(), falsificado).AbrirAsync(e.Envio.Id, default));
        await using (var db = Context())
        { (await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id)).Desactivar(); await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => HandlerRecepcion(e).ConsultarAsync(e.Envio.Id));
    }

    [PostgresFact]
    public async Task RecepcionesConcurrentesNoPierdenEscaneosYExigenReleerParaOtroBulto()
    {
        var e = await PrepararRecepcion(varios: true);
        var servicio = HandlerRecepcion(e);
        var version = (await servicio.ConsultarAsync(e.Envio.Id)).Version;
        var handler = new RecepcionDepositoHandler(new BarreraRecepcion(RepositorioRecepcion(e)),
            new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(1))));
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => handler.EscanearAsync(e.Envio.Id, new("B001", version))),
            Record.ExceptionAsync(() => handler.EscanearAsync(e.Envio.Id, new("B002", version))));
        Assert.Single(errores, e => e is null);
        Assert.IsType<ConflictoConcurrenciaException>(Assert.Single(errores, e => e is not null));
        var estado = await servicio.ConsultarAsync(e.Envio.Id);
        var cargado = Assert.Single(estado.Bultos, b => b.Recibido);
        Assert.True((await servicio.EscanearAsync(e.Envio.Id, new(cargado.Codigo, version))).Repetido);
        var pendiente = estado.Bultos.First(b => !b.Recibido);
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => servicio.EscanearAsync(e.Envio.Id, new(pendiente.Codigo, version)));
        Assert.False((await servicio.EscanearAsync(e.Envio.Id, new(pendiente.Codigo, estado.Version))).Repetido);
        await using var db = Context();
        Assert.Equal(2, await db.Set<EscaneoBulto>().CountAsync());
    }

    [PostgresFact]
    public async Task ConfirmacionesConcurrentesGeneranUnSoloEventoYMensajeDeRecepcion()
    {
        var e = await PrepararRecepcion();
        var cargado = await HandlerRecepcion(e).EscanearAsync(e.Envio.Id, new("B001", (await HandlerRecepcion(e).ConsultarAsync(e.Envio.Id)).Version));
        var handler = new RecepcionDepositoHandler(new BarreraRecepcion(RepositorioRecepcion(e)),
            new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(1))));
        var solicitud = new ConfirmarRecepcionCommand(cargado.Recepcion.Version);
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => handler.ConfirmarAsync(e.Envio.Id, solicitud)),
            Record.ExceptionAsync(() => handler.ConfirmarAsync(e.Envio.Id, solicitud)));
        Assert.Single(errores, e => e is null);
        Assert.IsType<ConflictoConcurrenciaException>(Assert.Single(errores, e => e is not null));
        await using var db = Context();
        Assert.Single(await db.Set<EventoDeEnvio>().Where(e => e.Tipo == TipoEvento.Validar).ToListAsync());
        Assert.Single(await db.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.Validar).ToListAsync());
    }

    [PostgresFact]
    public Task RecepcionFalloDeEscaneoRevierteVersionYPermiteReintentar() => VerificarRollbackRecepcion("escaneo");
    [PostgresFact]
    public Task RecepcionFalloDeDiscrepanciaRevierteVersionYPermiteReintentar() => VerificarRollbackRecepcion("discrepancia");
    [PostgresFact]
    public Task RecepcionFalloDeOutboxRevierteEstadoEventoYVersion() => VerificarRollbackRecepcion("confirmacion");

    private async Task VerificarRollbackRecepcion(string operacion)
    {
        var e = await PrepararRecepcion();
        var handler = HandlerRecepcion(e);
        var antes = await handler.ConsultarAsync(e.Envio.Id);
        if (operacion == "confirmacion")
            antes = (await handler.EscanearAsync(e.Envio.Id, new("B001", antes.Version))).Recepcion;
        var (agregarRestriccion, quitarRestriccion) = operacion switch {
            "escaneo" => ("ALTER TABLE \"EscaneosBulto\" ADD CONSTRAINT \"FalloRecepcion\" CHECK (false) NOT VALID",
                "ALTER TABLE \"EscaneosBulto\" DROP CONSTRAINT \"FalloRecepcion\""),
            "discrepancia" => ("ALTER TABLE \"Discrepancias\" ADD CONSTRAINT \"FalloRecepcion\" CHECK (false) NOT VALID",
                "ALTER TABLE \"Discrepancias\" DROP CONSTRAINT \"FalloRecepcion\""),
            _ => ("ALTER TABLE \"MensajesOutbox\" ADD CONSTRAINT \"FalloRecepcion\" CHECK (false) NOT VALID",
                "ALTER TABLE \"MensajesOutbox\" DROP CONSTRAINT \"FalloRecepcion\"") };
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync(agregarRestriccion);
        Task Ejecutar() => operacion switch {
            "escaneo" => handler.EscanearAsync(e.Envio.Id, new("B001", antes.Version)),
            "discrepancia" => handler.RegistrarDiscrepanciaAsync(e.Envio.Id, new(antes.Version, TipoDiscrepancia.PesoNoCoincide, "Peso", "2 kg", "3 kg")),
            _ => handler.ConfirmarAsync(e.Envio.Id, new(antes.Version)) };
        await Assert.ThrowsAsync<DbUpdateException>(Ejecutar);
        var despues = await handler.ConsultarAsync(e.Envio.Id);
        Assert.Equal(antes.Version, despues.Version);
        Assert.Equal(EstadoEnvio.Admitido, despues.Estado);
        Assert.Equal(antes.Recibidos, despues.Recibidos);
        Assert.Empty(despues.Discrepancias);
        await using (var db = Context())
        {
            Assert.Empty(await db.Set<EventoDeEnvio>().Where(e => e.Tipo == TipoEvento.Validar).ToListAsync());
            Assert.Empty(await db.Set<MensajeOutbox>().Where(m => m.Evento.Tipo == TipoEvento.Validar).ToListAsync());
            await db.Database.ExecuteSqlRawAsync(quitarRestriccion);
        }
        await Ejecutar();
        Assert.NotEqual(antes.Version, (await handler.ConsultarAsync(e.Envio.Id)).Version);
    }

    private sealed class BarreraRecepcion(IRecepcionDepositoRepository inner) : IRecepcionDepositoRepository
    {
        private int _abiertas;
        private readonly TaskCompletionSource _listas = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IOperacionRecepcionDeposito> AbrirAsync(Guid id, CancellationToken ct)
        {
            var op = await inner.AbrirAsync(id, ct);
            if (Interlocked.Increment(ref _abiertas) == 2) _listas.TrySetResult();
            try { await _listas.Task.WaitAsync(TimeSpan.FromSeconds(20), ct); return op; }
            catch { await op.DisposeAsync(); throw; }
        }
    }
}
