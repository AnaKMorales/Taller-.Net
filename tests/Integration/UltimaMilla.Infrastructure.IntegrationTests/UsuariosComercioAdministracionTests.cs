using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Infrastructure.Persistence.Administracion;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task GestionUsuarioComercioModificaEmailConservaCredencialYBajaRevocaSesionExistente()
    {
        var e = await PrepararApiComercios();
        var id = e.Envio.CuentaComercial.Usuarios.Single().Id;
        var cuenta = e.Envio.CuentaComercial.Id;
        using var servidor = new ServidorApi(_testConnection); using var admin = servidor.Cliente(e, e.Admin);
        await Antiforgery(admin);
        var url = $"/api/administracion/comercios/{cuenta}/usuarios/{id}";
        using var respuesta = await admin.PutAsJsonAsync(url, new ModificarUsuarioComercio(" NUEVO@example.com ", new("usuario@example.com", true)));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var editado = (await respuesta.Content.ReadFromJsonAsync<UsuarioComercioAdministracionDto>())!;
        Assert.Equal("nuevo@example.com", editado.Email); Assert.True(editado.Activo);
        var login = new BuscadorUsuariosLogin(Options());
        Assert.Null(await login.ValidarAsync("usuario@example.com", ClaveApiComercio));
        Assert.Equal(id, (await login.ValidarAsync(editado.Email, ClaveApiComercio))!.UsuarioId);
        using var real = ServidorComercios(new()); using var comercio = real.Cliente();
        await LoginApiComercio(comercio, editado.Email);
        Assert.Equal(HttpStatusCode.OK, (await comercio.GetAsync("/api/comercios/envios/opciones")).StatusCode);
        using var baja = await admin.PostAsJsonAsync(url + "/baja", new EstadoUsuarioEsperado(editado.Email, true));
        Assert.Equal(HttpStatusCode.OK, baja.StatusCode);
        Assert.False((await baja.Content.ReadFromJsonAsync<UsuarioComercioAdministracionDto>())!.Activo);
        Assert.Null(await login.ValidarAsync(editado.Email, ClaveApiComercio));
        Assert.Equal(HttpStatusCode.Forbidden, (await comercio.GetAsync("/api/comercios/envios/opciones")).StatusCode);
        await using var db = Context();
        var guardado = await db.Set<Usuario>().Include(u => u.CuentasComerciales).SingleAsync(u => u.Id == id);
        Assert.Equal(RolUsuario.Comercio, guardado.Rol); Assert.Equal(cuenta, Assert.Single(guardado.CuentasComerciales).Id);
        Assert.Equal(e.Envio.Id, (await db.Envios.SingleAsync()).Id);
    }

    [PostgresFact]
    public async Task GestionUsuarioComercioValidaPermisosCsrfDuplicadosYEstadoLeido()
    {
        var e = await PrepararAdministracionFlota();
        var usuario = e.Envio.CuentaComercial.Usuarios.Single();
        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin); using var anonimo = servidor.Cliente(); using var repartidor = servidor.Cliente(e);
        var url = $"/api/administracion/comercios/{e.Envio.CuentaComercial.Id}/usuarios/{usuario.Id}";
        var cambio = new ModificarUsuarioComercio("otro@example.com", new(usuario.Email, true));
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PutAsJsonAsync(url, cambio)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.PostAsJsonAsync(url + "/baja", cambio.Esperado)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(url, cambio)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url + "/baja", cambio.Esperado)).StatusCode);
        await Antiforgery(admin);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(url, cambio with { Email = " ADMIN@example.com " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(url, cambio with { Email = "invalido" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(url, cambio with { Esperado = new(usuario.Email, null) })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(url, cambio with { Esperado = new("viejo@example.com", true) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(url + "/baja", cambio.Esperado)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(url, cambio)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(url, cambio with { Esperado = new(usuario.Email, false) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(url + "/baja", new EstadoUsuarioEsperado(usuario.Email, false))).StatusCode);
    }

    [PostgresFact]
    public async Task GestionUsuarioComercioRechazaCuentaAjenaOtrosRolesYUsuarioConVariasCuentas()
    {
        var e = await PrepararAdministracionFlota();
        var gestion = new AdministracionComercios(Options(), TenantFlota(e));
        var usuario = e.Envio.CuentaComercial.Usuarios.Single(); var estado = new EstadoUsuarioEsperado(usuario.Email, true);
        var otraCuenta = await gestion.CrearAsync(new("Segunda", "123", "c@example.com"));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => gestion.ModificarUsuarioAsync(otraCuenta.CuentaId, usuario.Id, new("x@example.com", estado)));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => gestion.DarBajaUsuarioAsync(e.Envio.CuentaComercial.Id, e.Admin.Id, new(e.Admin.Email, true)));
        var otro = new EscenarioPersistencia();
        await using (var db = Context(otro.Operador.Id)) { db.Add(otro.Envio); await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => gestion.DarBajaUsuarioAsync(otro.Envio.CuentaComercial.Id,
            otro.Envio.CuentaComercial.Usuarios.Single().Id, estado));
        await using (var db = Context())
        {
            var cuenta = await db.Set<CuentaComercial>().Include(c => c.Operador).SingleAsync(c => c.Id == otraCuenta.CuentaId);
            (await db.Set<Usuario>().Include(u => u.Operador).Include(u => u.CuentasComerciales).SingleAsync(u => u.Id == usuario.Id)).AsignarCuentaComercial(cuenta);
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => gestion.DarBajaUsuarioAsync(e.Envio.CuentaComercial.Id, usuario.Id, estado));
        await using var lectura = Context();
        Assert.True((await lectura.Set<Usuario>().SingleAsync(u => u.Id == usuario.Id)).Activo);
        (await lectura.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id)).Desactivar(); await lectura.SaveChangesAsync();
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => gestion.ModificarUsuarioAsync(e.Envio.CuentaComercial.Id, usuario.Id, new("x@example.com", estado)));
    }

    [PostgresFact]
    public async Task GestionUsuarioComercioEdicionYBajaConcurrentesNoSeSobrescriben()
    {
        var e = await PrepararAdministracionFlota();
        var gestion = new AdministracionComercios(Options(), TenantFlota(e));
        var usuario = e.Envio.CuentaComercial.Usuarios.Single(); var estado = new EstadoUsuarioEsperado(usuario.Email, true);
        var errores = await Task.WhenAll(
            Record.ExceptionAsync(() => gestion.ModificarUsuarioAsync(e.Envio.CuentaComercial.Id, usuario.Id, new("carrera@example.com", estado))),
            Record.ExceptionAsync(() => gestion.DarBajaUsuarioAsync(e.Envio.CuentaComercial.Id, usuario.Id, estado)));
        Assert.Single(errores, e => e is null);
        Assert.IsType<ConflictoConcurrenciaException>(Assert.Single(errores, e => e is not null));
        var actual = Assert.Single(await gestion.UsuariosAsync(e.Envio.CuentaComercial.Id));
        Assert.True(actual.Activo ? actual.Email == "carrera@example.com" : actual.Email == usuario.Email);
    }

    [PostgresFact]
    public async Task GestionUsuarioComercioFalloDeGuardadoConservaEmailEstadoYCredenciales()
    {
        var e = await PrepararApiComercios();
        var gestion = new AdministracionComercios(Options(), TenantFlota(e));
        var usuario = e.Envio.CuentaComercial.Usuarios.Single(); var estado = new EstadoUsuarioEsperado(usuario.Email, true);
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Usuarios\" ADD CONSTRAINT \"FalloEdicionUsuario\" CHECK (\"Activo\" AND \"Email\" <> 'rollback@example.com') NOT VALID");
        await Assert.ThrowsAsync<DbUpdateException>(() => gestion.ModificarUsuarioAsync(e.Envio.CuentaComercial.Id, usuario.Id, new("rollback@example.com", estado)));
        await Assert.ThrowsAsync<DbUpdateException>(() => gestion.DarBajaUsuarioAsync(e.Envio.CuentaComercial.Id, usuario.Id, estado));
        Assert.Equal(usuario.Id, (await new BuscadorUsuariosLogin(Options()).ValidarAsync(usuario.Email, ClaveApiComercio))!.UsuarioId);
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Usuarios\" DROP CONSTRAINT \"FalloEdicionUsuario\"");
        Assert.False((await gestion.DarBajaUsuarioAsync(e.Envio.CuentaComercial.Id, usuario.Id, estado)).Activo);
    }

    [PostgresFact]
    public async Task GestionUsuarioComercioDosEdicionesNoCompartenUnEmailAunqueCompitan()
    {
        var e = await PrepararAdministracionFlota();
        var normal = new AdministracionComercios(Options(), TenantFlota(e));
        var cuenta = e.Envio.CuentaComercial.Id;
        var segundo = await normal.CrearUsuarioAsync(cuenta, new("segundo@example.com", "Contrasena123!"));
        var primero = e.Envio.CuentaComercial.Usuarios.Single();
        var opciones = new DbContextOptionsBuilder<AppDbContext>(Options()).AddInterceptors(new BarreraAltaFlota()).Options;
        var gestion = new AdministracionComercios(opciones, TenantFlota(e));
        var errores = await Task.WhenAll(
            Record.ExceptionAsync(() => gestion.ModificarUsuarioAsync(cuenta, primero.Id, new("unico@example.com", new(primero.Email, true)))),
            Record.ExceptionAsync(() => gestion.ModificarUsuarioAsync(cuenta, segundo.Id, new("unico@example.com", new(segundo.Email, true)))));
        Assert.Single(errores, e => e is null);
        Assert.IsType<InvalidOperationException>(Assert.Single(errores, e => e is not null));
        Assert.Single(await normal.UsuariosAsync(cuenta), u => u.Email == "unico@example.com");
        Assert.Equal(2, (await normal.UsuariosAsync(cuenta)).Count);
    }
}
