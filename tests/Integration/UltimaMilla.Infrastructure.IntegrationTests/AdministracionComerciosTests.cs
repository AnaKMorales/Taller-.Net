using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Administracion;
using UltimaMilla.Infrastructure.Persistence.Tenancy;
using UltimaMilla.Web.Api;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task ApiAdministracionComerciosCreaCuentaYUsuarioQuePuedeDarAltaPorApi()
    {
        var e = await PrepararAdministracionFlota();
        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin); using var anonimo = servidor.Cliente(); using var repartidor = servidor.Cliente(e);
        const string url = "/api/administracion/comercios";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(url)).StatusCode);
        var solicitud = new CrearComercio(" Comercio nuevo ", " 099000009 ", " CONTACTO@nuevo.example ");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url, solicitud)).StatusCode);
        await Antiforgery(admin);
        using var alta = await admin.PostAsJsonAsync(url, solicitud);
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var cuenta = (await alta.Content.ReadFromJsonAsync<CuentaAdministracionDto>())!;
        Assert.Equal(("Comercio nuevo", "099000009", "contacto@nuevo.example"), (cuenta.RazonSocial, cuenta.Telefono, cuenta.Email));
        Assert.Contains((await admin.GetFromJsonAsync<CuentaAdministracionDto[]>(url))!, c => c.CuentaId == cuenta.CuentaId);
        var usuariosUrl = $"{url}/{cuenta.CuentaId}/usuarios";
        Assert.Empty((await admin.GetFromJsonAsync<UsuarioComercioAdministracionDto[]>(usuariosUrl))!);
        using var nuevo = await admin.PostAsJsonAsync(usuariosUrl, new CrearUsuarioComercio(" USUARIO@nuevo.example ", ClaveApiComercio));
        Assert.Equal(HttpStatusCode.Created, nuevo.StatusCode);
        Assert.DoesNotContain(ClaveApiComercio, await nuevo.Content.ReadAsStringAsync());
        var usuario = (await nuevo.Content.ReadFromJsonAsync<UsuarioComercioAdministracionDto>())!;
        Assert.True(usuario.Activo);
        Assert.Equal("usuario@nuevo.example", usuario.Email);
        Assert.Equal(usuario.Id, Assert.Single((await admin.GetFromJsonAsync<UsuarioComercioAdministracionDto[]>(usuariosUrl))!).Id);
        using var real = ServidorComercios(new() { Ahora = EscenarioPersistencia.Fecha });
        using var cliente = real.Cliente(); await LoginApiComercio(cliente, usuario.Email);
        var envio = await CotizarApiComercio(cliente, e.Zona.Id);
        using var creado = await cliente.PostAsJsonAsync("/api/comercios/envios", envio, JsonApi);
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var resultado = (await creado.Content.ReadFromJsonAsync<ResultadoAltaComercio>(JsonApi))!;
        await using var db = Context();
        Assert.Equal(cuenta.CuentaId, (await db.Envios.Include(x => x.CuentaComercial).SingleAsync(x =>
            x.CodigoSeguimiento == resultado.Envio.CodigoSeguimiento)).CuentaComercialId);
        var guardado = await db.Set<Usuario>().Include(u => u.CuentasComerciales).SingleAsync(u => u.Id == usuario.Id);
        Assert.Equal(RolUsuario.Comercio, guardado.Rol);
        Assert.Equal(cuenta.CuentaId, Assert.Single(guardado.CuentasComerciales).Id);
        Assert.NotEqual(ClaveApiComercio, (await db.Set<CuentaAcceso>().SingleAsync(c => c.Id == usuario.Id)).PasswordHash);
    }

    [PostgresFact]
    public async Task ApiAdministracionComerciosRechazaDatosInvalidosEmailsDuplicadosYCuentaAjena()
    {
        var e = await PrepararAdministracionFlota();
        using var servidor = new ServidorApi(_testConnection); using var admin = servidor.Cliente(e, e.Admin);
        await Antiforgery(admin);
        const string url = "/api/administracion/comercios";
        foreach (var solicitud in new[] { new CrearComercio(" ", "123", "a@b.example"),
            new("Válido", " ", "a@b.example"), new("Válido", "123", "no-email") })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url, solicitud)).StatusCode);
        var usuariosUrl = $"{url}/{e.Envio.CuentaComercial.Id}/usuarios";
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(usuariosUrl,
            new CrearUsuarioComercio(" ADMIN@example.com ", "Contrasena123!"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(usuariosUrl,
            new CrearUsuarioComercio("nuevo@example.com", "corta"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"{url}/{Guid.NewGuid()}/usuarios",
            new CrearUsuarioComercio("nuevo@example.com", "Contrasena123!"))).StatusCode);
        await using var db = Context();
        Assert.Single(await db.Set<CuentaComercial>().ToListAsync());
        Assert.DoesNotContain(await db.Set<Usuario>().ToListAsync(), u => u.Email == "nuevo@example.com");
    }

    [PostgresFact]
    public async Task AdministracionComerciosRespetaComercioCompartidoYCuentasSeparadas()
    {
        var e = await PrepararAdministracionFlota();
        var otro = new Operador("Otro operador");
        var adminOtro = new Usuario(otro, "otro-admin@example.com", RolUsuario.Administrador);
        await using (var db = Context(otro.Id)) { db.Add(adminOtro); await db.SaveChangesAsync(); }
        Guid cuentaAjena;
        await using (var db = Context(otro.Id))
        {
            // Preparación privilegiada del caso compartido existente, no una operación de la API.
            var compartido = await db.Set<Comercio>().IgnoreQueryFilters().SingleAsync(c => c.Id == e.Envio.ComercioId);
            var cuenta = new CuentaComercial(await db.Set<Operador>().SingleAsync(), compartido);
            db.Add(cuenta); await db.SaveChangesAsync(); cuentaAjena = cuenta.Id;
        }
        var propio = new AdministracionComercios(Options(), TenantFlota(e));
        var ajeno = new AdministracionComercios(Options(), new TenantActual(TenantContext.ParaUsuario(otro.Id, adminOtro.Id, RolUsuario.Administrador)));
        Assert.Equal(e.Envio.CuentaComercial.Id, Assert.Single(await propio.CuentasAsync()).CuentaId);
        Assert.Equal(cuentaAjena, Assert.Single(await ajeno.CuentasAsync()).CuentaId);
        Assert.Equal(e.Envio.ComercioId, Assert.Single(await ajeno.CuentasAsync()).ComercioId);
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.CrearUsuarioAsync(cuentaAjena, new("intruso@example.com", "Contrasena123!")));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.UsuariosAsync(cuentaAjena));
        var usuario = e.Envio.CuentaComercial.Usuarios.Single();
        var restringido = new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, usuario.Id, RolUsuario.Comercio, e.Envio.CuentaComercial.Id));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => new AdministracionComercios(Options(), restringido).CuentasAsync());
        await using (var db = Context())
        { (await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id)).Desactivar(); await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.CrearAsync(new("Revocado", "123", "r@example.com")));
    }

    [PostgresFact]
    public async Task AdministracionComerciosUsuariosConcurrentesNoCompartenNiDuplicanIdentidad()
    {
        var e = await PrepararAdministracionFlota();
        var normal = new AdministracionComercios(Options(), TenantFlota(e));
        var segunda = await normal.CrearAsync(new("Segunda", "123", "s@example.com"));
        var options = new DbContextOptionsBuilder<AppDbContext>(Options()).AddInterceptors(new BarreraAltaFlota()).Options;
        var gestion = new AdministracionComercios(options, TenantFlota(e));
        var solicitud = new CrearUsuarioComercio("carrera-comercio@example.com", "Contrasena123!");
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => gestion.CrearUsuarioAsync(e.Envio.CuentaComercial.Id, solicitud)),
            Record.ExceptionAsync(() => gestion.CrearUsuarioAsync(segunda.CuentaId, solicitud)));
        Assert.Single(errores, x => x is null);
        Assert.IsType<InvalidOperationException>(Assert.Single(errores, x => x is not null));
        await using var db = Context();
        var creado = Assert.Single(await db.Set<Usuario>().Include(u => u.CuentasComerciales).Where(u => u.Email == solicitud.Email).ToListAsync());
        Assert.Single(creado.CuentasComerciales);
    }

    [PostgresFact]
    public async Task AdministracionComerciosFalloDeCuentaRevierteTambienElComercioNuevo()
    {
        var e = await PrepararAdministracionFlota();
        var gestion = new AdministracionComercios(Options(), TenantFlota(e));
        int comercios;
        await using (var db = Context())
        {
            comercios = await db.Set<Comercio>().IgnoreQueryFilters().CountAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"CuentasComerciales\" ADD CONSTRAINT \"FalloCuenta\" CHECK (false) NOT VALID");
        }
        var solicitud = new CrearComercio("Nuevo", "123", "c@example.com");
        await Assert.ThrowsAsync<DbUpdateException>(() => gestion.CrearAsync(solicitud));
        await using (var db = Context())
        {
            Assert.Equal(comercios, await db.Set<Comercio>().IgnoreQueryFilters().CountAsync());
            Assert.Single(await db.Set<CuentaComercial>().ToListAsync());
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"CuentasComerciales\" DROP CONSTRAINT \"FalloCuenta\"");
        }
        Assert.NotEqual(Guid.Empty, (await gestion.CrearAsync(solicitud)).CuentaId);
    }

    [PostgresFact]
    public async Task AdministracionComerciosFalloDeVinculoRevierteUsuarioYCredenciales()
    {
        var e = await PrepararAdministracionFlota();
        var gestion = new AdministracionComercios(Options(), TenantFlota(e));
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"UsuariosCuentasComerciales\" ADD CONSTRAINT \"FalloUsuarioCuenta\" CHECK (false) NOT VALID");
        var solicitud = new CrearUsuarioComercio("rollback-comercio@example.com", "Contrasena123!");
        await Assert.ThrowsAsync<DbUpdateException>(() => gestion.CrearUsuarioAsync(e.Envio.CuentaComercial.Id, solicitud));
        await using (var db = Context())
        {
            Assert.False(await db.Set<Usuario>().AnyAsync(u => u.Email == solicitud.Email));
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"UsuariosCuentasComerciales\" DROP CONSTRAINT \"FalloUsuarioCuenta\"");
        }
        var creado = await gestion.CrearUsuarioAsync(e.Envio.CuentaComercial.Id, solicitud);
        Assert.Equal(creado.Id, (await new BuscadorUsuariosLogin(Options()).ValidarAsync(solicitud.Email, solicitud.Contrasena))!.UsuarioId);
    }
}
