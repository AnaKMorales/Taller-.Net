using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    private static IdentidadOperadorDto MarcaPrueba(string marca = " Marca nueva ") =>
        new(marca, "#aabbcc", "#123456", " 099111222 ", " CONTACTO@example.com ", "https://marca.example/logo.png");

    [PostgresFact]
    public async Task A2ApiAdminConfiguraIdentidadYLasLecturasDeComercioYTokenRecibenDatosSegurosActuales()
    {
        var e = await PrepararApiComercios();
        using var servidor = new ServidorApi(_testConnection); using var admin = servidor.Cliente(e, e.Admin);
        using var anonimo = servidor.Cliente(); using var repartidor = servidor.Cliente(e);
        const string url = "/api/administracion/identidad";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(url)).StatusCode);
        var estado = (await admin.GetFromJsonAsync<EstadoIdentidadOperador>(url))!;
        Assert.Equal(e.Operador.RazonSocial, estado.Datos.Marca);
        var solicitud = new ModificarIdentidadOperador(MarcaPrueba(), estado.Version);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(url, solicitud)).StatusCode);
        await Antiforgery(admin);
        var publicoUrl = $"/api/seguimiento/{e.Envio.CodigoSeguimiento}/identidad";
        Assert.Equal(estado.Datos, await anonimo.GetFromJsonAsync<IdentidadOperadorDto>(publicoUrl));
        using var respuesta = await admin.PutAsJsonAsync(url, solicitud);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var actualizado = (await respuesta.Content.ReadFromJsonAsync<EstadoIdentidadOperador>())!;
        Assert.NotEqual(estado.Version, actualizado.Version);
        Assert.Equal("Marca nueva", actualizado.Datos.Marca); Assert.Equal("#AABBCC", actualizado.Datos.ColorPrimario);
        Assert.Equal("contacto@example.com", actualizado.Datos.EmailContacto);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(url, solicitud)).StatusCode);
        using var publica = await anonimo.GetAsync(publicoUrl);
        Assert.True(publica.Headers.CacheControl!.NoStore);
        Assert.Equal(actualizado.Datos, await publica.Content.ReadFromJsonAsync<IdentidadOperadorDto>());
        var json = JsonDocument.Parse(await publica.Content.ReadAsStringAsync());
        Assert.Equal(6, json.RootElement.EnumerateObject().Count());
        Assert.DoesNotContain(e.Operador.Id.ToString(), await publica.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync("/api/seguimiento/invalido/identidad")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"/api/seguimiento/{Guid.NewGuid():N}/identidad")).StatusCode);
        using var real = ServidorComercios(new()); using var comercio = real.Cliente(); await LoginApiComercio(comercio);
        Assert.Equal(actualizado.Datos, await comercio.GetFromJsonAsync<IdentidadOperadorDto>("/api/operador/identidad"));
        Assert.Equal(HttpStatusCode.Forbidden, (await comercio.PutAsJsonAsync(url, solicitud)).StatusCode);
        await using var db = Context(); Assert.Equal(e.Operador.RazonSocial, (await db.Set<Operador>().SingleAsync()).RazonSocial);
    }

    [PostgresFact]
    public async Task A2IdentidadAislaOperadoresRevalidaUsuariosYPermiteSoloLecturaDelMensajeConfiable()
    {
        var e = await PrepararApiComercios(); var otro = new EscenarioPersistencia();
        await using (var db = Context(otro.Operador.Id)) { db.Add(otro.Envio); await db.SaveChangesAsync(); }
        var propio = new IdentidadOperador(Options(), TenantFlota(e));
        var ajeno = new IdentidadOperador(Options(), TenantFlota(otro));
        var original = await propio.ConsultarAsync(); var estadoAjeno = await ajeno.ConsultarAsync();
        var cambio = await propio.ModificarAsync(new(MarcaPrueba("Propia"), original.Version));
        Assert.Equal(estadoAjeno, await ajeno.ConsultarAsync());
        Assert.Equal(cambio.Datos, await propio.PorSeguimientoAsync(e.Envio.CodigoSeguimiento));
        Assert.Equal(estadoAjeno.Datos, await propio.PorSeguimientoAsync(otro.Envio.CodigoSeguimiento));
        var mensaje = new IdentidadOperador(Options(), new TenantActual(await new TenantResolver(Options()).DesdeMensajeAsync(new(e.Operador.Id, e.Envio.CuentaComercial.Id))));
        Assert.Equal(cambio.Datos, await mensaje.ActualAsync());
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => mensaje.ModificarAsync(new(MarcaPrueba(), cambio.Version)));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => new IdentidadOperador(Options(), TenantActual.ParaOperador(e.Operador.Id)).ActualAsync());
        var falsificado = new IdentidadOperador(Options(), new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Repartidor.Id, RolUsuario.Administrador)));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => falsificado.ModificarAsync(new(MarcaPrueba(), cambio.Version)));
        await using (var db = Context()) { (await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id)).Desactivar(); await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.ConsultarAsync());
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.ModificarAsync(new(MarcaPrueba(), cambio.Version)));
    }

    [PostgresFact]
    public async Task A2IdentidadRechazaVersionYDatosInvalidosSinCambioParcial()
    {
        var e = await PrepararAdministracionFlota(); var gestion = new IdentidadOperador(Options(), TenantFlota(e));
        var original = await gestion.ConsultarAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => gestion.ModificarAsync(new(MarcaPrueba(), Guid.Empty)));
        foreach (var invalida in new[] { MarcaPrueba() with { Marca = new string('x', 201) }, MarcaPrueba() with { ColorPrimario = "var(--color)" },
            MarcaPrueba() with { TelefonoContacto = new string('x', 51) }, MarcaPrueba() with { EmailContacto = "invalido" },
            MarcaPrueba() with { LogoUrl = "data:image/svg+xml,script" }, MarcaPrueba() with { LogoUrl = new string('x', 2049) } })
            await Assert.ThrowsAsync<ArgumentException>(() => gestion.ModificarAsync(new(invalida, original.Version)));
        Assert.Equal(original, await gestion.ConsultarAsync());
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Operadores\" ADD CONSTRAINT \"FalloIdentidad\" CHECK (\"Marca\" <> 'Rechazar') NOT VALID");
        await Assert.ThrowsAsync<DbUpdateException>(() => gestion.ModificarAsync(new(MarcaPrueba("Rechazar"), original.Version)));
        Assert.Equal(original, await gestion.ConsultarAsync());
        await using (var db = Context()) await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Operadores\" DROP CONSTRAINT \"FalloIdentidad\"");
    }

    [PostgresFact]
    public async Task A2IdentidadDosAdministradoresNoSobrescribenLaMismaVersion()
    {
        var e = await PrepararAdministracionFlota();
        var gestion = new IdentidadOperador(Options(), TenantFlota(e)); var original = await gestion.ConsultarAsync();
        var opciones = new DbContextOptionsBuilder<AppDbContext>(Options()).AddInterceptors(new BarreraAltaFlota()).Options;
        var paralela = new IdentidadOperador(opciones, TenantFlota(e));
        var errores = await Task.WhenAll(Record.ExceptionAsync(() => paralela.ModificarAsync(new(MarcaPrueba("A"), original.Version))),
            Record.ExceptionAsync(() => paralela.ModificarAsync(new(MarcaPrueba("B"), original.Version))));
        Assert.Single(errores, x => x is null); Assert.IsType<ConflictoConcurrenciaException>(Assert.Single(errores, x => x is not null));
        Assert.Contains((await gestion.ConsultarAsync()).Datos.Marca, new[] { "A", "B" });
    }

    [PostgresFact]
    public async Task A2IdentidadMigracionMantieneRazonSocialYNoInventaContactoPrevio()
    {
        var e = await PrepararAdministracionFlota();
        await using (var db = Context())
        {
            var anterior = db.Database.GetMigrations().TakeWhile(m => !m.EndsWith("_IdentidadVisualOperador", StringComparison.Ordinal)).Last();
            await db.GetService<IMigrator>().MigrateAsync(anterior); await db.Database.MigrateAsync();
        }
        var estado = await new IdentidadOperador(Options(), TenantFlota(e)).ConsultarAsync();
        Assert.Equal(e.Operador.RazonSocial, estado.Datos.Marca); Assert.NotEqual(Guid.Empty, estado.Version);
        Assert.Null(estado.Datos.EmailContacto); Assert.Null(estado.Datos.TelefonoContacto); Assert.Null(estado.Datos.LogoUrl);
        Assert.Equal("#1E40AF", estado.Datos.ColorPrimario);
        await using var lectura = Context(); Assert.Single(await lectura.Envios.ToListAsync());
    }
}
