using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Administracion;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task CuentaComercialApiModificaDatosPropiosYBajaRevocaSesionSinBorrarHistorial()
    {
        var e = await PrepararApiComercios();
        using var servidor = new ServidorApi(_testConnection); using var admin = servidor.Cliente(e, e.Admin);
        const string url = "/api/administracion/comercios";
        var cuenta = Assert.Single((await admin.GetFromJsonAsync<CuentaAdministracionDto[]>(url))!);
        var cambio = new ModificarCuentaComercial(" Sucursal nueva ", " 099999999 ", " CONTACTO@nuevo.example ", cuenta.Version);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"{url}/{cuenta.CuentaId}", cambio)).StatusCode);
        await Antiforgery(admin);
        using var respuesta = await admin.PutAsJsonAsync($"{url}/{cuenta.CuentaId}", cambio);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var modificada = (await respuesta.Content.ReadFromJsonAsync<CuentaAdministracionDto>())!;
        Assert.Equal(("Sucursal nueva", "099999999", "contacto@nuevo.example"), (modificada.RazonSocial, modificada.Telefono, modificada.Email));
        Assert.NotEqual(cuenta.Version, modificada.Version);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{url}/{cuenta.CuentaId}/baja", new BajaCuentaComercial(cuenta.Version))).StatusCode);
        using var real = ServidorComercios(new()); using var comercio = real.Cliente(); await LoginApiComercio(comercio);
        using var baja = await admin.PostAsJsonAsync($"{url}/{cuenta.CuentaId}/baja", new BajaCuentaComercial(modificada.Version));
        Assert.Equal(HttpStatusCode.OK, baja.StatusCode);
        Assert.False((await baja.Content.ReadFromJsonAsync<CuentaAdministracionDto>())!.Activa);
        Assert.Equal(HttpStatusCode.Forbidden, (await comercio.GetAsync("/api/comercios/envios/opciones")).StatusCode);
        Assert.Null(await new BuscadorUsuariosLogin(Options()).ValidarAsync("usuario@example.com", ClaveApiComercio));
        var gestion = new AdministracionComercios(Options(), TenantFlota(e));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gestion.CrearUsuarioAsync(cuenta.CuentaId, new("otro@example.com", ClaveApiComercio)));
        await using var db = Context();
        Assert.Single(await db.Envios.ToListAsync());
        Assert.True((await db.Set<Usuario>().SingleAsync(u => u.Email == "usuario@example.com")).Activo);
        Assert.Equal(e.Envio.TarifaCalculada, (await db.Envios.SingleAsync()).TarifaCalculada);
        Assert.Equal("Comercio", (await db.Set<Comercio>().SingleAsync()).RazonSocial);
    }

    [PostgresFact]
    public async Task CuentaComercialCompartidaNoCambiaDatosNiAccesoDelOtroOperador()
    {
        var e = await PrepararApiComercios();
        var otro = new Operador("Otro"); var adminOtro = new Usuario(otro, "otro-admin@example.com", RolUsuario.Administrador);
        Guid cuentaAjena;
        await using (var db = Context(otro.Id))
        {
            var compartido = await db.Set<Comercio>().IgnoreQueryFilters().SingleAsync(c => c.Id == e.Envio.ComercioId);
            var cuenta = new CuentaComercial(otro, compartido); cuentaAjena = cuenta.Id;
            var user = new Usuario(otro, "otro-comercio@example.com", RolUsuario.Comercio); user.AsignarCuentaComercial(cuenta);
            db.AddRange(cuenta, adminOtro, user); await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, user, ClaveApiComercio); await db.SaveChangesAsync();
        }
        var propio = new AdministracionComercios(Options(), TenantFlota(e));
        var ajeno = new AdministracionComercios(Options(), new TenantActual(TenantContext.ParaUsuario(otro.Id, adminOtro.Id, RolUsuario.Administrador)));
        var original = Assert.Single(await propio.CuentasAsync());
        var editada = await propio.ModificarCuentaAsync(original.CuentaId, new("Local", "123", "local@example.com", original.Version));
        await propio.DarBajaCuentaAsync(original.CuentaId, new(editada.Version));
        var otra = Assert.Single(await ajeno.CuentasAsync());
        Assert.True(otra.Activa); Assert.Equal("Comercio", otra.RazonSocial); Assert.Equal(original.ComercioId, otra.ComercioId);
        Assert.NotNull(await new BuscadorUsuariosLogin(Options()).ValidarAsync("otro-comercio@example.com", ClaveApiComercio));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.DarBajaCuentaAsync(cuentaAjena, new(otra.Version)));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.ModificarCuentaAsync(cuentaAjena, new("X", "123", "x@example.com", otra.Version)));
    }

    [PostgresFact]
    public async Task CuentaComercialDosEdicionesConcurrentesSoloUnaConfirma()
    {
        var e = await PrepararAdministracionFlota();
        var normal = new AdministracionComercios(Options(), TenantFlota(e)); var cuenta = Assert.Single(await normal.CuentasAsync());
        var opciones = new DbContextOptionsBuilder<AppDbContext>(Options()).AddInterceptors(new BarreraAltaFlota()).Options;
        var gestion = new AdministracionComercios(opciones, TenantFlota(e));
        var errores = await Task.WhenAll(
            Record.ExceptionAsync(() => gestion.ModificarCuentaAsync(cuenta.CuentaId, new("Nueva", "123", "x@example.com", cuenta.Version))),
            Record.ExceptionAsync(() => gestion.DarBajaCuentaAsync(cuenta.CuentaId, new(cuenta.Version))));
        Assert.Single(errores, x => x is null);
        Assert.IsType<ConflictoConcurrenciaException>(Assert.Single(errores, x => x is not null));
        var final = Assert.Single(await normal.CuentasAsync());
        Assert.True(final.Activa ? final.RazonSocial == "Nueva" : final.RazonSocial == cuenta.RazonSocial);
    }

    [PostgresFact]
    public async Task CuentaComercialInactivaBloqueaEscrituraConContextoViejoPeroPermiteProcesarEnviosPrevios()
    {
        var e = await PrepararApiComercios();
        var tenant = TenantContext.ParaUsuario(e.Operador.Id, e.Envio.CuentaComercial.Usuarios.Single().Id,
            RolUsuario.Comercio, e.Envio.CuentaComercial.Id);
        await using var viejo = new AppDbContext(Options(), new TenantActual(tenant));
        var anterior = await EscenarioPersistencia.Grafo(viejo).SingleAsync();
        var gestion = new AdministracionComercios(Options(), TenantFlota(e)); var cuenta = Assert.Single(await gestion.CuentasAsync());
        await gestion.DarBajaCuentaAsync(cuenta.CuentaId, new(cuenta.Version));
        viejo.Entry(anterior.Destinatario).Property(d => d.Nombre).CurrentValue = "No autorizado";
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => viejo.SaveChangesAsync());
        await using var admin = Context();
        var envio = await EscenarioPersistencia.Grafo(admin).SingleAsync();
        var responsable = await admin.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id);
        foreach (var b in envio.Bultos) EscaneoBulto.RegistrarRecepcion(b, responsable, EscenarioPersistencia.Fecha.AddMinutes(1));
        envio.RecibirEnDeposito(responsable, EscenarioPersistencia.Fecha.AddMinutes(1)); await admin.SaveChangesAsync();
        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
        // Protección incluso ante un INSERT directo que elude los servicios y usa una cuenta inactiva.
        var error = await Assert.ThrowsAsync<PostgresException>(() => admin.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"Envios\" SELECT * FROM \"Envios\" WHERE \"Id\" = {envio.Id}"));
        Assert.Equal("CK_Envios_CuentaActiva", error.ConstraintName);
        var mensaje = await new TenantResolver(Options()).DesdeMensajeAsync(new(e.Operador.Id, cuenta.CuentaId));
        Assert.Equal(cuenta.CuentaId, mensaje.CuentaComercialId);
    }

    [PostgresFact]
    public async Task CuentaComercialMigracionRecuperaDatosExistentesYFalloDeCambioNoDejaBajaParcial()
    {
        var e = await PrepararAdministracionFlota();
        await using (var db = Context())
        {
            var anterior = db.Database.GetMigrations().TakeWhile(m => !m.EndsWith("_AdministracionCuentaComercial", StringComparison.Ordinal)).Last();
            await db.GetService<IMigrator>().MigrateAsync(anterior);
            await db.Database.MigrateAsync();
        }
        var gestion = new AdministracionComercios(Options(), TenantFlota(e)); var cuenta = Assert.Single(await gestion.CuentasAsync());
        Assert.Equal((e.Envio.CuentaComercial.Comercio.RazonSocial, e.Envio.CuentaComercial.Comercio.Telefono, e.Envio.CuentaComercial.Comercio.Email),
            (cuenta.RazonSocial, cuenta.Telefono, cuenta.Email));
        Assert.True(cuenta.Activa); Assert.NotEqual(Guid.Empty, cuenta.Version);
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"CuentasComerciales\" ADD CONSTRAINT \"FalloBajaCuenta\" CHECK (\"Activa\" AND \"RazonSocial\" <> 'Rechazar') NOT VALID");
        await Assert.ThrowsAsync<DbUpdateException>(() => gestion.DarBajaCuentaAsync(cuenta.CuentaId, new(cuenta.Version)));
        await Assert.ThrowsAsync<DbUpdateException>(() => gestion.ModificarCuentaAsync(cuenta.CuentaId, new("Rechazar", "123", "x@example.com", cuenta.Version)));
        Assert.Equal(cuenta, Assert.Single(await gestion.CuentasAsync()));
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"CuentasComerciales\" DROP CONSTRAINT \"FalloBajaCuenta\"");
    }
}
