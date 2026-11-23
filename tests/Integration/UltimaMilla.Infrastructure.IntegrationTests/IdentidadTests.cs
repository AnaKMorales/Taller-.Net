using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Administracion;
using UltimaMilla.Infrastructure.Persistence.Tenancy;
using UltimaMilla.Web.Api;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Infrastructure.IntegrationTests;

// ASP.NET Identity como almacén de usuarios (ADR-008): login, bloqueo, altas, migración y la excepción del interceptor.
public sealed partial class PostgresTests
{
    private const string ClaveIdentidad = "Identidad-123!";
    private const string MigracionAnteriorAIdentidad = "20261114215630_IdentidadVisualOperador";

    private async Task<EscenarioPersistencia> PrepararIdentidad()
    {
        var e = Escenario();
        await using var db = Context();
        db.Add(e.Envio);
        foreach (var usuario in new Usuario[] { e.Admin, e.Repartidor, e.Envio.CuentaComercial.Usuarios.Single() })
            await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, usuario, ClaveIdentidad);
        await db.SaveChangesAsync();
        return e;
    }

    // Login de la web (Razor Page con antiforgery), como lo hace el navegador.
    private static async Task<HttpResponseMessage> LoginWeb(HttpClient cliente, string email, string clave)
    {
        var html = await cliente.GetStringAsync("/login");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return await cliente.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email, ["Contrasena"] = clave, ["__RequestVerificationToken"] = token
        }));
    }

    private AppDbContext SinTenant() => new(Options());

    [PostgresFact]
    public async Task IdentidadLosTresRolesEntranConCookieOBearerYLosClaimsNoCambian()
    {
        var e = await PrepararIdentidad();
        var comercio = e.Envio.CuentaComercial.Usuarios.Single();
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false);

        // Administrador: cookie y backoffice.
        using (var admin = servidor.Cliente())
        {
            using var login = await LoginWeb(admin, e.Admin.Email, ClaveIdentidad);
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal("/backoffice/tablero", login.Headers.Location?.OriginalString);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/administracion/flota/repartidores")).StatusCode);
        }
        // Comercio: cookie y portal; y Bearer de la API de comercios con su cuenta.
        using (var portal = servidor.Cliente())
        {
            using var login = await LoginWeb(portal, comercio.Email, ClaveIdentidad);
            Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
            Assert.Equal("/portal", login.Headers.Location?.OriginalString);
        }
        using (var api = servidor.Cliente())
        {
            using var sesion = await api.PostAsJsonAsync("/api/comercios/sesion", new LoginComercio(comercio.Email, ClaveIdentidad));
            Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);
            var token = (await sesion.Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
            api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.OK, (await api.GetAsync("/api/comercios/envios/opciones")).StatusCode);
        }
        // Repartidor: Bearer de la PWA; perfil con su Id (claims operador_id + rol resueltos por TenantResolver).
        using (var pwa = servidor.Cliente())
        {
            using var sesion = await pwa.PostAsJsonAsync("/api/repartidor/sesion", new SolicitudLoginRepartidor(e.Repartidor.Email, ClaveIdentidad));
            Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);
            var token = (await sesion.Content.ReadFromJsonAsync<JsonObject>())!["accessToken"]!.GetValue<string>();
            pwa.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var perfil = (await pwa.GetFromJsonAsync<PerfilRepartidor>("/api/repartidor/sesion"))!;
            Assert.Equal(e.Repartidor.Id, perfil.UsuarioId);
        }
        // El repartidor sigue sin poder entrar a la web.
        using (var web = servidor.Cliente())
        {
            using var login = await LoginWeb(web, e.Repartidor.Email, ClaveIdentidad);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            Assert.Contains("aplicación del repartidor", WebUtility.HtmlDecode(await login.Content.ReadAsStringAsync()));
            Assert.Equal(HttpStatusCode.Unauthorized, (await web.GetAsync("/api/administracion/flota/repartidores")).StatusCode);
        }

        // Los datos de UsuarioLogin (de donde salen los claims) son los de siempre.
        var datos = (await new BuscadorUsuariosLogin(Options()).ValidarAsync(comercio.Email, ClaveIdentidad))!;
        Assert.Equal((comercio.Id, e.Operador.Id, RolUsuario.Comercio, (Guid?)e.Envio.CuentaComercial.Id),
            (datos.UsuarioId, datos.OperadorId, datos.Rol, datos.CuentaComercialId));
    }

    [PostgresFact]
    public async Task IdentidadClaveIncorrectaEmailInexistenteYCuentaBloqueadaDanLaMismaRespuesta()
    {
        var e = await PrepararIdentidad();
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false);
        async Task<string> Intento(string email, string clave)
        {
            using var cliente = servidor.Cliente();
            using var respuesta = await LoginWeb(cliente, email, clave);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
            Assert.Contains("Email o contraseña incorrectos.", html);
            return Regex.Match(html, "Email o contraseña incorrectos\\.").Value;
        }

        var inexistente = await Intento("nadie@example.com", ClaveIdentidad);
        var incorrecta = await Intento(e.Admin.Email, "Otra-clave-1!");
        Assert.Equal(inexistente, incorrecta);
        // Cuatro fallos más (cinco en total) bloquean la cuenta 5 minutos.
        for (var i = 0; i < 4; i++) await Intento(e.Admin.Email, "Otra-clave-1!");
        // Bloqueada: ni con la clave correcta entra, y la respuesta es la misma.
        Assert.Equal(inexistente, await Intento(e.Admin.Email, ClaveIdentidad));

        await using (var db = SinTenant())
        {
            var cuenta = await db.Set<CuentaAcceso>().AsNoTracking().SingleAsync(c => c.Id == e.Admin.Id);
            Assert.NotNull(cuenta.LockoutEnd);
            var resta = cuenta.LockoutEnd!.Value - DateTimeOffset.UtcNow;
            Assert.InRange(resta, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));
            Assert.Equal(0, cuenta.AccessFailedCount);
        }
        // La API del repartidor también responde igual (401) para los tres casos.
        using var pwa = servidor.Cliente();
        foreach (var (email, clave) in new[] { ("nadie@example.com", ClaveIdentidad), (e.Repartidor.Email, "Otra-clave-1!") })
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await pwa.PostAsJsonAsync("/api/repartidor/sesion", new SolicitudLoginRepartidor(email, clave))).StatusCode);

        // Pasado el bloqueo, la clave correcta vuelve a entrar y el contador queda en cero.
        await using (var db = Context())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"AspNetUsers\" SET \"LockoutEnd\" = {DateTimeOffset.UtcNow.AddSeconds(-1)} WHERE \"Id\" = {e.Admin.Id}");
        Assert.NotNull(await new BuscadorUsuariosLogin(Options()).ValidarAsync(e.Admin.Email, ClaveIdentidad));
    }

    [PostgresFact]
    public async Task IdentidadUnFalloSeCuentaYUnLoginCorrectoReiniciaElContador()
    {
        var e = await PrepararIdentidad();
        var login = new BuscadorUsuariosLogin(Options());
        Assert.Null(await login.ValidarAsync(e.Admin.Email, "Otra-clave-1!"));
        Assert.Null(await login.ValidarAsync(e.Admin.Email, "Otra-clave-1!"));
        await using (var db = SinTenant())
            Assert.Equal(2, (await db.Set<CuentaAcceso>().AsNoTracking().SingleAsync(c => c.Id == e.Admin.Id)).AccessFailedCount);
        Assert.NotNull(await login.ValidarAsync(e.Admin.Email, ClaveIdentidad));
        await using (var db = SinTenant())
            Assert.Equal(0, (await db.Set<CuentaAcceso>().AsNoTracking().SingleAsync(c => c.Id == e.Admin.Id)).AccessFailedCount);
    }

    [PostgresFact]
    public async Task IdentidadEmailDeOtroOperadorSeRechazaConMensajeGenericoYLaPoliticaExigeComplejidad()
    {
        var e = await PrepararIdentidad();
        var otro = new Operador("Otro operador");
        var adminOtro = new Usuario(otro, "admin-otro@example.com", RolUsuario.Administrador);
        await using (var db = Context(otro.Id))
        {
            db.Add(adminOtro);
            await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, adminOtro, ClaveIdentidad);
            await db.SaveChangesAsync();
        }
        var flotaOtro = new AdministracionFlota(Options(),
            new TenantActual(TenantContext.ParaUsuario(otro.Id, adminOtro.Id, RolUsuario.Administrador)));

        // El email ya lo usa el administrador del primer operador: mensaje genérico, sin decir dónde existe.
        var duplicado = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            flotaOtro.CrearRepartidorAsync(new CrearRepartidor(" ADMIN@example.com ", ClaveIdentidad)));
        Assert.Equal(FabricaCuentasAcceso.MensajeEmailNoDisponible, duplicado.Message);
        Assert.DoesNotContain("operador", duplicado.Message, StringComparison.OrdinalIgnoreCase);
        await using (var db = Context(otro.Id))
            Assert.Single(await db.Set<Usuario>().ToListAsync()); // no quedó nada a medias

        // Política: 8+ con mayúscula, minúscula, número y símbolo. Demo1234! es válida.
        // (Menos de 8 ya lo frena la validación de largo previa del caso de uso.)
        await Assert.ThrowsAsync<ArgumentException>(() =>
            flotaOtro.CrearRepartidorAsync(new CrearRepartidor($"r{Guid.NewGuid():N}@example.com", "Corta1!")));
        foreach (var debil in new[] { "sinmayuscula1!", "SINMINUSCULA1!", "SinNumero!!", "SinSimbolo123" })
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() =>
                flotaOtro.CrearRepartidorAsync(new CrearRepartidor($"r{Guid.NewGuid():N}@example.com", debil)));
            Assert.Equal(FabricaCuentasAcceso.MensajeContrasena, error.Message);
        }
        var creado = await flotaOtro.CrearRepartidorAsync(new CrearRepartidor("nuevo-repartidor@example.com", "Demo1234!"));
        Assert.Equal(creado.Id, (await new BuscadorUsuariosLogin(Options()).ValidarAsync("nuevo-repartidor@example.com", "Demo1234!"))!.UsuarioId);
    }

    [PostgresFact]
    public async Task IdentidadUnUsuarioDeUnOperadorNoVeLosDatosDeOtro()
    {
        var e = await PrepararIdentidad();
        var otro = new Operador("Otro operador");
        var adminOtro = new Usuario(otro, "admin-otro@example.com", RolUsuario.Administrador);
        var repartidorOtro = new Repartidor(otro, "repartidor-otro@example.com");
        await using (var db = Context(otro.Id))
        {
            db.AddRange(adminOtro, repartidorOtro);
            await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, adminOtro, ClaveIdentidad);
            await db.SaveChangesAsync();
        }
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false);
        using var admin = servidor.Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginWeb(admin, e.Admin.Email, ClaveIdentidad)).StatusCode);
        var propios = (await admin.GetFromJsonAsync<RepartidorAdministracionDto[]>("/api/administracion/flota/repartidores"))!;
        Assert.Contains(propios, r => r.Id == e.Repartidor.Id);
        Assert.DoesNotContain(propios, r => r.Id == repartidorOtro.Id);

        using var deOtro = servidor.Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await LoginWeb(deOtro, adminOtro.Email, ClaveIdentidad)).StatusCode);
        var ajenos = (await deOtro.GetFromJsonAsync<RepartidorAdministracionDto[]>("/api/administracion/flota/repartidores"))!;
        Assert.Equal([repartidorOtro.Id], ajenos.Select(r => r.Id));
    }

    [PostgresFact]
    public async Task IdentidadSinTenantSoloSeGuardanIntentosBloqueoYStamps()
    {
        var e = await PrepararIdentidad();

        // Permitido: lo que guarda el login (intentos, bloqueo, stamps).
        await using (var db = SinTenant())
        {
            var cuenta = await db.Set<CuentaAcceso>().SingleAsync(c => c.Id == e.Admin.Id);
            cuenta.AccessFailedCount = 3;
            cuenta.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(1);
            cuenta.SecurityStamp = Guid.NewGuid().ToString("N");
            cuenta.ConcurrencyStamp = Guid.NewGuid().ToString();
            Assert.Equal(1, await db.SaveChangesAsync());
        }

        // Prohibido todo lo demás, una por una.
        var prohibidas = new (string Caso, Action<AppDbContext, CuentaAcceso> Cambio)[]
        {
            ("contraseña", (_, c) => c.PasswordHash = "otro-hash"),
            ("usuario", (_, c) => c.UserName = c.NormalizedUserName = "intruso@example.com"),
            ("email", (_, c) => c.Email = "intruso@example.com"),
            ("desactivar bloqueo", (_, c) => c.LockoutEnabled = false),
            ("borrar la cuenta", (db, c) => db.Remove(c)),
            ("intentos junto con otra entidad", (db, c) =>
            {
                c.AccessFailedCount = 1;
                db.Set<Usuario>().IgnoreQueryFilters().Single(u => u.Id == c.Id).Desactivar();
            }),
            ("cuenta nueva", (db, _) => db.Add(new CuentaAcceso { Id = Guid.NewGuid(), UserName = "x@example.com" })),
        };
        foreach (var (caso, cambio) in prohibidas)
        {
            await using var db = SinTenant();
            var cuenta = await db.Set<CuentaAcceso>().SingleAsync(c => c.Id == e.Admin.Id);
            cambio(db, cuenta);
            var error = await Record.ExceptionAsync(() => db.SaveChangesAsync());
            Assert.True(error is AislamientoOperadorException, $"Sin tenant se guardó: {caso} ({error?.GetType().Name})");
        }
        // Y cualquier otra escritura sin tenant sigue fallando, también la sincrónica.
        await using (var db = SinTenant())
        {
            db.Set<Usuario>().IgnoreQueryFilters().Single(u => u.Id == e.Admin.Id).Desactivar();
            Assert.Throws<AislamientoOperadorException>(() => db.SaveChanges());
        }
        await using (var db = SinTenant())
            Assert.Equal(3, (await db.Set<CuentaAcceso>().AsNoTracking().SingleAsync(c => c.Id == e.Admin.Id)).AccessFailedCount);
    }

    [PostgresFact]
    public async Task IdentidadOtroOperadorNoPuedeTocarLaCuentaDeAccesoAjena()
    {
        var e = await PrepararIdentidad();
        var otro = new Operador("Otro operador");
        await using (var db = Context(otro.Id)) { db.Add(new Usuario(otro, "admin-otro@example.com", RolUsuario.Administrador)); await db.SaveChangesAsync(); }

        await using (var db = Context(otro.Id))
        {
            // La tabla de Identity no tiene filtro: la lectura funciona, pero la escritura se valida por el Usuario dueño.
            var ajena = await db.Set<CuentaAcceso>().SingleAsync(c => c.Id == e.Admin.Id);
            ajena.PasswordHash = "robada";
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        await using (var db = Context(otro.Id))
        {
            db.Add(new CuentaAcceso { Id = e.Repartidor.Id, UserName = "otra@example.com" });
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        // Una sesión de comercio no administra credenciales, ni las propias.
        var comercio = e.Envio.CuentaComercial.Usuarios.Single();
        await using (var db = new AppDbContext(Options(), new TenantActual(
            TenantContext.ParaUsuario(e.Operador.Id, comercio.Id, RolUsuario.Comercio, e.Envio.CuentaComercial.Id))))
        {
            (await db.Set<CuentaAcceso>().SingleAsync(c => c.Id == comercio.Id)).PasswordHash = "otro";
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
    }

    [PostgresFact]
    public async Task IdentidadElHashViejoEntraConUserManagerSinRehashAntesYDespuesDeMigrar()
    {
        // Hash generado exactamente como antes de Identity: PasswordHasher<Usuario> con opciones por defecto.
        var e = Escenario();
        var hashViejo = new PasswordHasher<Usuario>().HashPassword(e.Admin, "Secreta123");
        var comprobador = FabricaCuentasAcceso.Predeterminada;
        await using (var db = SinTenant())
        using (var usuarios = comprobador.Para(db))
        {
            var cuenta = new CuentaAcceso { PasswordHash = hashViejo };
            Assert.Equal(PasswordVerificationResult.Success,
                usuarios.PasswordHasher.VerifyHashedPassword(cuenta, hashViejo, "Secreta123"));
            Assert.True(await usuarios.CheckPasswordAsync(cuenta, "Secreta123"));
            Assert.False(await usuarios.CheckPasswordAsync(cuenta, "secreta123"));
        }

        // Sembrar con el modelo actual y después volver al esquema que guardaba el hash en Usuarios.ContrasenaHash.
        await using (var db = Context())
        {
            db.Add(e.Envio);
            db.Entry(e.Admin).Property<string?>(ContrasenasUsuario.Columna).CurrentValue = hashViejo;
            await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync(MigracionAnteriorAIdentidad);
        }
        await using (var db = Context()) await db.Database.MigrateAsync();

        var login = await new BuscadorUsuariosLogin(Options()).ValidarAsync("admin@example.com", "Secreta123");
        Assert.Equal(e.Admin.Id, login?.UsuarioId);
        await using (var db = SinTenant())
        {
            var cuenta = await db.Set<CuentaAcceso>().AsNoTracking().SingleAsync(c => c.Id == e.Admin.Id);
            Assert.Equal(hashViejo, cuenta.PasswordHash); // no se rehasheó
            Assert.Equal(("ADMIN@EXAMPLE.COM", "admin@example.com", true), (cuenta.NormalizedUserName, cuenta.Email, cuenta.LockoutEnabled));
            // ContrasenaHash queda como respaldo.
            Assert.Equal(hashViejo, await db.Set<Usuario>().IgnoreQueryFilters().Where(u => u.Id == e.Admin.Id)
                .Select(u => EF.Property<string?>(u, ContrasenasUsuario.Columna)).SingleAsync());
            // Los usuarios sin contraseña también tienen su cuenta, pero no pueden entrar.
            Assert.Null((await db.Set<CuentaAcceso>().AsNoTracking().SingleAsync(c => c.Id == e.Repartidor.Id)).PasswordHash);
        }
        Assert.Null(await new BuscadorUsuariosLogin(Options()).ValidarAsync("repartidor@example.com", "Secreta123"));
    }

    [PostgresFact]
    public async Task IdentidadMigracionConEmailsRepetidosFallaConMensajeClaroYNoDejaNadaAMedias()
    {
        // Sembrar los usuarios del dominio con el modelo actual. Los duplicados entre operadores
        // reproducen los datos que podía contener la base anterior a Identity.
        foreach (var _ in new[] { 1, 2 })
        {
            var e = Escenario();
            await using var db = Context();
            db.Add(e.Envio);
            await db.SaveChangesAsync();
        }

        await using (var db = Context())
            await db.GetService<IMigrator>().MigrateAsync(MigracionAnteriorAIdentidad);

        await using (var db = Context())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
            Assert.Contains("emails repetidos entre operadores", error.MessageText);
            Assert.Contains("ADMIN@EXAMPLE.COM", error.MessageText);
        }
        await using (var db = Context())
        {
            Assert.Contains("20261115190814_AgregarIdentidad", await db.Database.GetPendingMigrationsAsync());
            await using var conexion = new NpgsqlConnection(_testConnection);
            await conexion.OpenAsync();
            await using var existe = new NpgsqlCommand("SELECT to_regclass('\"AspNetUsers\"') IS NOT NULL", conexion);
            Assert.False((bool)(await existe.ExecuteScalarAsync())!);
        }
    }
}
