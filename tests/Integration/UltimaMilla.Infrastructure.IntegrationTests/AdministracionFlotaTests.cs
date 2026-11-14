using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Administracion;
using UltimaMilla.Infrastructure.Persistence.Tenancy;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private async Task<EscenarioPersistencia> PrepararAdministracionFlota()
    {
        var e = Escenario();
        await using var db = Context(); db.Add(e.Envio); await db.SaveChangesAsync();
        return e;
    }
    private static TenantActual TenantFlota(EscenarioPersistencia e) =>
        new(TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador));

    [PostgresFact]
    public async Task ApiFlotaCreaListaYPermiteLoginPwaYCrearRutaConLosNuevosRecursos()
    {
        var e = await PrepararAdministracionFlota();
        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin);
        using var anonimo = servidor.Cliente(); using var repartidor = servidor.Cliente(e);
        const string url = "/api/administracion/flota";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(url + "/vehiculos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(url + "/repartidores")).StatusCode);
        var alta = new CrearRepartidor(" NUEVO@example.com ", "Repartidor-nuevo-123!");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url + "/repartidores", alta)).StatusCode);
        await Antiforgery(admin);
        using var respuesta = await admin.PostAsJsonAsync(url + "/repartidores", alta);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var nuevo = (await respuesta.Content.ReadFromJsonAsync<RepartidorAdministracionDto>())!;
        Assert.Equal("nuevo@example.com", nuevo.Email); Assert.True(nuevo.Activo);
        Assert.DoesNotContain(alta.Contrasena, await respuesta.Content.ReadAsStringAsync());
        using var vehiculo = await admin.PostAsJsonAsync(url + "/vehiculos", new CrearVehiculo(" abc-999 ", 350, 8));
        Assert.Equal(HttpStatusCode.Created, vehiculo.StatusCode);
        var camioneta = (await vehiculo.Content.ReadFromJsonAsync<VehiculoAdministracionDto>())!;
        Assert.Equal(("ABC-999", 350m, 8m), (camioneta.Matricula, camioneta.CapacidadPesoKg, camioneta.CapacidadVolumenM3));
        Assert.Contains((await admin.GetFromJsonAsync<RepartidorAdministracionDto[]>(url + "/repartidores"))!, r => r.Id == nuevo.Id);
        Assert.Contains((await admin.GetFromJsonAsync<VehiculoAdministracionDto[]>(url + "/vehiculos"))!, v => v.Id == camioneta.Id);
        var recursos = (await admin.GetFromJsonAsync<RecursosAsignacionDto>("/api/despacho/recursos"))!;
        Assert.Contains(recursos.Repartidores, r => r.Id == nuevo.Id);
        Assert.Contains(recursos.Vehiculos, v => v.Id == camioneta.Id);
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/despacho/rutas",
            new CrearRutaPendientes(nuevo.Id, camioneta.Id, 10))).StatusCode);
        using var real = new ServidorApi(_testConnection, usarIdentidadPrueba: false);
        using var pwa = real.Cliente();
        using var sesion = await pwa.PostAsJsonAsync("/api/repartidor/sesion", new SolicitudLoginRepartidor(nuevo.Email, alta.Contrasena));
        Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);
        await using var lectura = Context();
        var hash = await lectura.Set<Usuario>().Where(u => u.Id == nuevo.Id).Select(u => EF.Property<string>(u, ContrasenasUsuario.Columna)).SingleAsync();
        Assert.NotEqual(alta.Contrasena, hash);
        Assert.Equal(e.Operador.Id, (await lectura.Set<Repartidor>().Include(r => r.Operador).SingleAsync(r => r.Id == nuevo.Id)).OperadorId);
    }

    [PostgresFact]
    public async Task ApiFlotaRechazaDatosInvalidosYDuplicadosNormalizadosIncluyendoEmailsDeOtrosRoles()
    {
        var e = await PrepararAdministracionFlota();
        using var servidor = new ServidorApi(_testConnection); using var admin = servidor.Cliente(e, e.Admin);
        await Antiforgery(admin);
        const string url = "/api/administracion/flota";
        foreach (var invalido in new[] { new CrearRepartidor("no-es-email", "Contrasena123!"), new("valido@example.com", "corta"),
            new("Nombre <email@example.com>", "Contrasena123!") })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url + "/repartidores", invalido)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url + "/repartidores", new CrearRepartidor(" ADMIN@example.com ", "Contrasena123!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(url + "/vehiculos", new CrearVehiculo(" abc123 ", 100, 10))).StatusCode);
        foreach (var invalido in new[] { new CrearVehiculo(" ", 1, 1), new("NUEVA", 0, 1), new("NUEVA", 1, -1) })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url + "/vehiculos", invalido)).StatusCode);
        Assert.DoesNotContain((await admin.GetFromJsonAsync<VehiculoAdministracionDto[]>(url + "/vehiculos"))!, v => v.Matricula == "NUEVA");
    }

    [PostgresFact]
    public async Task AdministracionFlotaAislaOperadoresYRevalidaElAdministrador()
    {
        var e = await PrepararAdministracionFlota();
        var otro = new EscenarioPersistencia();
        await using (var db = Context(otro.Operador.Id)) { db.Add(otro.Envio); await db.SaveChangesAsync(); }
        var propio = new AdministracionFlota(Options(), TenantFlota(e));
        var ajeno = new AdministracionFlota(Options(), TenantFlota(otro));
        var a = await propio.CrearVehiculoAsync(new("COMPARTIDA", 10, 1));
        var b = await ajeno.CrearVehiculoAsync(new("COMPARTIDA", 10, 1));
        Assert.NotEqual(a.Id, b.Id);
        Assert.DoesNotContain(await propio.VehiculosAsync(), v => v.Id == b.Id);
        Assert.DoesNotContain(await ajeno.VehiculosAsync(), v => v.Id == a.Id);
        foreach (var tenant in new[] { TenantActual.ParaOperador(e.Operador.Id),
            new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Repartidor.Id, RolUsuario.Administrador)),
            new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Despachador)) })
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => new AdministracionFlota(Options(), tenant).VehiculosAsync());
        await using (var db = Context())
        { (await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id)).Desactivar(); await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.CrearVehiculoAsync(new("REVOCADO", 1, 1)));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.RepartidoresAsync());
    }

    [PostgresFact]
    public Task FlotaAltasConcurrentesNoDuplicanEmailNiCredenciales() => VerificarCarreraFlota(true);
    [PostgresFact]
    public Task FlotaAltasConcurrentesNoDuplicanMatricula() => VerificarCarreraFlota(false);

    private async Task VerificarCarreraFlota(bool repartidor)
    {
        var e = await PrepararAdministracionFlota();
        var opciones = new DbContextOptionsBuilder<AppDbContext>(Options()).AddInterceptors(new BarreraAltaFlota()).Options;
        var gestion = new AdministracionFlota(opciones, TenantFlota(e));
        Task Crear() => repartidor ? gestion.CrearRepartidorAsync(new("carrera@example.com", "Contrasena123!")) :
            gestion.CrearVehiculoAsync(new("CARRERA", 10, 1));
        var errores = await Task.WhenAll(Record.ExceptionAsync(Crear), Record.ExceptionAsync(Crear));
        Assert.Single(errores, x => x is null);
        Assert.IsType<InvalidOperationException>(Assert.Single(errores, x => x is not null));
        await using var db = Context();
        if (repartidor) Assert.Single(await db.Set<Repartidor>().Where(r => r.Email == "carrera@example.com").ToListAsync());
        else Assert.Single(await db.Set<Vehiculo>().Where(v => v.Matricula == "CARRERA").ToListAsync());
    }

    [PostgresFact]
    public async Task FlotaFalloDePersistenciaNoDejaUnRepartidorSinSusCredenciales()
    {
        var e = await PrepararAdministracionFlota();
        var gestion = new AdministracionFlota(Options(), TenantFlota(e));
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Usuarios\" ADD CONSTRAINT \"FalloFlota\" CHECK (false) NOT VALID");
        var solicitud = new CrearRepartidor("rollback@example.com", "Contrasena123!");
        await Assert.ThrowsAsync<DbUpdateException>(() => gestion.CrearRepartidorAsync(solicitud));
        Assert.DoesNotContain(await gestion.RepartidoresAsync(), r => r.Email == solicitud.Email);
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Usuarios\" DROP CONSTRAINT \"FalloFlota\"");
        var creado = await gestion.CrearRepartidorAsync(solicitud);
        Assert.Equal(creado.Id, (await new BuscadorUsuariosLogin(Options()).ValidarAsync(solicitud.Email, solicitud.Contrasena))!.UsuarioId);
    }

    private sealed class BarreraAltaFlota : SaveChangesInterceptor
    {
        private int _listas;
        private readonly TaskCompletionSource _ambas = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _listas) == 2) _ambas.TrySetResult();
            await _ambas.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            return result;
        }
    }
}
