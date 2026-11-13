using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Despacho;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task ApiCargaCompletaElRecorridoHastaDespachoYDescargaDeHoja()
    {
        var (e, rutaId) = await PrepararDespacho(cargar: false);
        Usuario deposito;
        await using (var db = Context())
        {
            deposito = new Usuario(await db.Set<Operador>().SingleAsync(), "deposito@example.com", RolUsuario.OperarioDeposito);
            db.Add(deposito); await db.SaveChangesAsync();
        }
        using var servidor = new ServidorApi(_testConnection);
        using var operario = servidor.Cliente(e, deposito);
        using var admin = servidor.Cliente(e, e.Admin);
        using var anonimo = servidor.Cliente();
        ((RelojConfiguracion)servidor.Services.GetRequiredService<TimeProvider>()).Ahora = EscenarioPersistencia.Fecha.AddHours(1);
        var url = $"/api/carga/rutas/{rutaId}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(url)).StatusCode);
        var antes = (await operario.GetFromJsonAsync<EstadoCargaRutaDto>(url, JsonApi))!;
        Assert.Equal((1, 0, 1), (antes.Esperados, antes.Cargados, antes.Faltantes));
        var solicitud = new RegistrarCargaRutaCommand(" b001 ", antes.Version);
        Assert.Equal(HttpStatusCode.BadRequest, (await operario.PostAsJsonAsync(url + "/bultos", solicitud)).StatusCode);
        await Antiforgery(operario);
        Assert.Equal(HttpStatusCode.BadRequest, (await operario.PostAsJsonAsync(url + "/bultos", solicitud with { Codigo = "ajeno" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(url + "/bultos", solicitud with { VersionEsperada = Guid.NewGuid() })).StatusCode);
        using var respuesta = await operario.PostAsJsonAsync(url + "/bultos", solicitud);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var carga = (await respuesta.Content.ReadFromJsonAsync<CargaRegistradaDto>(JsonApi))!;
        Assert.False(carga.Repetido);
        Assert.Equal(0, carga.Faltantes);
        using var repeticion = await operario.PostAsJsonAsync(url + "/bultos", solicitud);
        Assert.True((await repeticion.Content.ReadFromJsonAsync<CargaRegistradaDto>(JsonApi))!.Repetido);
        Assert.Equal(HttpStatusCode.Forbidden, (await operario.PostAsJsonAsync($"/api/despacho/rutas/{rutaId}/despachar", new DespacharRutaCommand(carga.Version))).StatusCode);
        await Antiforgery(admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/despacho/rutas/{rutaId}/despachar", new DespacharRutaCommand(carga.Version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await operario.PostAsJsonAsync(url + "/bultos", solicitud)).StatusCode);
        var hojas = new HojasRuta(Options(), new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Repartidor.Id, RolUsuario.Repartidor)));
        Assert.NotNull(await hojas.ObtenerAsync(rutaId));
        await using var lectura = Context();
        Assert.Single(await lectura.Set<EscaneoBulto>().Where(s => s.Tipo == TipoEscaneo.Carga).ToListAsync());
    }

    [PostgresFact]
    public async Task ApiCargaBearerSoloPermiteLaRutaDelRepartidorActivo()
    {
        var (e, rutaId) = await PrepararDespacho(cargar: false);
        await AsignarContrasenaIntentosAsync(e.Operador.Id, e.Repartidor.Id);
        var operadorAjeno = new Operador("Ajeno");
        var adminAjeno = new Usuario(operadorAjeno, "admin-ajeno@example.com", RolUsuario.Administrador);
        await using (var db = Context(operadorAjeno.Id)) { db.Add(adminAjeno); await db.SaveChangesAsync(); }
        Repartidor otro;
        await using (var db = Context())
        {
            otro = new Repartidor(await db.Set<Operador>().SingleAsync(), "otro@example.com");
            db.Add(otro); await db.SaveChangesAsync();
        }
        await AsignarContrasenaIntentosAsync(e.Operador.Id, otro.Id);
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false);
        using var propio = servidor.Cliente();
        using var noAsignado = servidor.Cliente();
        await IngresarIntentosAsync(propio, e.Repartidor.Email);
        await IngresarIntentosAsync(noAsignado, otro.Email);
        var url = $"/api/repartidor/carga/rutas/{rutaId}";
        Assert.Equal(HttpStatusCode.Forbidden, (await noAsignado.GetAsync(url)).StatusCode);
        var estado = (await propio.GetFromJsonAsync<EstadoCargaRutaDto>(url, JsonApi))!;
        using var respuesta = await propio.PostAsJsonAsync(url + "/bultos", new RegistrarCargaRutaCommand("B001", estado.Version));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode); // Bearer no requiere cookie CSRF.
        await using (var db = Context())
        {
            var usuario = await db.Set<Repartidor>().SingleAsync(r => r.Id == e.Repartidor.Id);
            usuario.Desactivar(); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await propio.GetAsync(url)).StatusCode);
        var otroTenant = new TenantActual(TenantContext.ParaUsuario(operadorAjeno.Id, adminAjeno.Id, RolUsuario.Administrador));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => new DespachoRutaRepository(Options(), otroTenant).AbrirCargaAsync(rutaId, default));
    }

    [PostgresFact]
    public async Task CargaConCodigoCompartidoExigeDesambiguarElBulto()
    {
        var (e, rutaId) = await PrepararDespacho(cargar: false);
        await using (var db = Context())
        {
            var original = await EscenarioPersistencia.Grafo(db).SingleAsync();
            var segundo = new Envio(original.CuentaComercial, new Destinatario("Otra", "099123456", "p@example.com",
                new Direccion("Calle", "2", "Montevideo", "11000")), [new Bulto("B001", 1, 10, 10, 10)],
                original.ModalidadServicio, original.Zona, original.Reglas, original.CuadroTarifario, 150,
                EscenarioPersistencia.Fecha, original.FechaComprometida, original.Programacion, original.Eventos.First().Responsable!);
            db.Add(segundo);
            await db.SaveChangesAsync();
            var actor = await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id);
            db.Add(EscaneoBulto.RegistrarRecepcion(segundo.Bultos.Single(), actor, EscenarioPersistencia.Fecha));
            segundo.RecibirEnDeposito(actor, EscenarioPersistencia.Fecha);
            var ruta = await db.Set<Ruta>().Include(r => r.Paradas).ThenInclude(p => p.Bultos).Include(r => r.Vehiculo)
                .SingleAsync(r => r.Id == rutaId);
            ruta.AgregarParada(segundo, 2, EscenarioPersistencia.Fecha.AddHours(1), actor, EscenarioPersistencia.Fecha);
            await db.SaveChangesAsync();
        }
        var handler = new RegistrarCargaRutaHandler(RepositorioDespacho(e), new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(1))));
        var estado = await handler.ConsultarAsync(rutaId);
        Assert.Equal(2, estado.Esperados);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(rutaId, new("B001", estado.Version)));
        var resultado = await handler.Handle(rutaId, new("B001", estado.Version, estado.Bultos[0].BultoId));
        Assert.Equal((1, 1), (resultado.Cargados, resultado.Faltantes));
    }

    [PostgresFact]
    public async Task CargaFallidaRevierteEscaneoYVersionesYPermiteReintentar()
    {
        var (e, rutaId) = await PrepararDespacho(cargar: false);
        var handler = new RegistrarCargaRutaHandler(RepositorioDespacho(e),
            new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(1))));
        var antes = await handler.ConsultarAsync(rutaId);
        Guid versionEnvio;
        await using (var db = Context())
        {
            versionEnvio = await db.Envios.Select(x => EF.Property<Guid>(x, "RowVersion")).SingleAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"EscaneosBulto\" ADD CONSTRAINT \"FalloCarga\" CHECK (false) NOT VALID");
        }
        var solicitud = new RegistrarCargaRutaCommand("B001", antes.Version);
        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(rutaId, solicitud));
        var despues = await handler.ConsultarAsync(rutaId);
        Assert.Equal(antes.Version, despues.Version);
        Assert.Equal(1, despues.Faltantes);
        await using (var db = Context())
        {
            Assert.Equal(versionEnvio, await db.Envios.Select(x => EF.Property<Guid>(x, "RowVersion")).SingleAsync());
            Assert.Empty(await db.Set<EscaneoBulto>().Where(x => x.Tipo == TipoEscaneo.Carga).ToListAsync());
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"EscaneosBulto\" DROP CONSTRAINT \"FalloCarga\"");
        }
        Assert.False((await handler.Handle(rutaId, solicitud)).Repetido);
    }

    [PostgresFact]
    public async Task CargasSimultaneasCompitenPorVersionYElReenvioNoDuplicaEscaneos()
    {
        var (e, rutaId) = await PrepararDespacho(cargar: false);
        var repositorio = new BarreraCarga(RepositorioDespacho(e));
        var reloj = new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(1)));
        var handler = new RegistrarCargaRutaHandler(repositorio, reloj);
        var servicio = new RegistrarCargaRutaHandler(RepositorioDespacho(e), reloj);
        var version = (await servicio.ConsultarAsync(rutaId)).Version;
        var solicitud = new RegistrarCargaRutaCommand("B001", version);
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => handler.Handle(rutaId, solicitud)),
            Record.ExceptionAsync(() => handler.Handle(rutaId, solicitud)));
        Assert.Single(errores, x => x is null);
        Assert.IsType<ConflictoConcurrenciaException>(Assert.Single(errores, x => x is not null));
        Assert.True((await servicio.Handle(rutaId, solicitud)).Repetido);
        await using var db = Context();
        Assert.Single(await db.Set<EscaneoBulto>().Where(s => s.Tipo == TipoEscaneo.Carga).ToListAsync());
    }

    private sealed class BarreraCarga(IRegistroCargaRutaRepository inner) : IRegistroCargaRutaRepository
    {
        private int _abiertas;
        private readonly TaskCompletionSource _listas = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IOperacionDespachoRuta> AbrirCargaAsync(Guid id, CancellationToken ct)
        {
            var op = await inner.AbrirCargaAsync(id, ct);
            if (Interlocked.Increment(ref _abiertas) == 2) _listas.TrySetResult();
            try { await _listas.Task.WaitAsync(TimeSpan.FromSeconds(20), ct); return op; }
            catch { await op.DisposeAsync(); throw; }
        }
    }
}
