using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Administracion;
using UltimaMilla.Web.Api;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private static CrearFranjaHoraria FranjaZona(string nombre = "Tarde") => new(nombre, new(14, 0), new(18, 0));
    private static CrearZonaCobertura SolicitudZona() => new(" Norte ", " 12000 ",
        [FranjaZona(), new(" Mañana ", new(8, 0), new(12, 0))]);

    [PostgresFact]
    public async Task ApiAdministracionZonasCreaListaYAgregaFranjasConAutorizacionYCsrf()
    {
        var e = await PrepararAdministracionFlota();
        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin); using var anonimo = servidor.Cliente();
        using var repartidor = servidor.Cliente(e);
        const string url = "/api/administracion/zonas";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.PostAsJsonAsync(url, SolicitudZona())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url, SolicitudZona())).StatusCode);
        await Antiforgery(admin);
        using var respuesta = await admin.PostAsJsonAsync(url, SolicitudZona());
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var zona = (await respuesta.Content.ReadFromJsonAsync<ZonaAdministracionDto>())!;
        Assert.Equal(("Norte", "12000"), (zona.Nombre, zona.CodigoPostal));
        Assert.Equal(new[] { "Mañana", "Tarde" }, zona.Franjas.Select(f => f.Nombre));
        Assert.Contains((await admin.GetFromJsonAsync<ZonaAdministracionDto[]>(url))!, z => z.Id == zona.Id);
        var franjasUrl = $"{url}/{zona.Id}/franjas";
        using var sinCsrf = servidor.Cliente(e, e.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await sinCsrf.PostAsJsonAsync(franjasUrl, FranjaZona())).StatusCode);
        using var creada = await admin.PostAsJsonAsync(franjasUrl, new CrearFranjaHoraria(" Noche ", new(19, 0), new(22, 0)));
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var franja = (await creada.Content.ReadFromJsonAsync<FranjaAdministracionDto>())!;
        Assert.Equal("Noche", franja.Nombre);
        var listado = (await admin.GetFromJsonAsync<FranjaAdministracionDto[]>(franjasUrl))!;
        Assert.Equal(3, listado.Length); Assert.Equal(franja.Id, listado.Last().Id);
        await using var db = Context();
        var guardada = await db.Set<ZonaCobertura>().Include(z => z.Operador).Include(z => z.Franjas).SingleAsync(z => z.Id == zona.Id);
        Assert.Equal(e.Operador.Id, guardada.OperadorId);
        Assert.All(guardada.Franjas, f => Assert.Equal(guardada.Id, f.ZonaId));
    }

    [PostgresFact]
    public async Task ApiAdministracionZonasRechazaTextosHorariosYListasInvalidasSinAltasParciales()
    {
        var e = await PrepararAdministracionFlota();
        using var servidor = new ServidorApi(_testConnection); using var admin = servidor.Cliente(e, e.Admin);
        await Antiforgery(admin);
        const string url = "/api/administracion/zonas";
        foreach (var solicitud in new[] { SolicitudZona() with { Nombre = " " }, SolicitudZona() with { CodigoPostal = " " },
            SolicitudZona() with { Nombre = new string('x', 201) }, SolicitudZona() with { CodigoPostal = new string('x', 21) },
            SolicitudZona() with { Franjas = [] }, SolicitudZona() with { Franjas = null! },
            SolicitudZona() with { Franjas = [FranjaZona(), null!] },
            SolicitudZona() with { Franjas = [FranjaZona(), new("Inválida", new(18, 0), new(8, 0))] } })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(url, solicitud)).StatusCode);
        foreach (var solicitud in new[] { FranjaZona() with { Nombre = " " }, FranjaZona() with { Nombre = new string('x', 201) },
            FranjaZona() with { Hasta = new(14, 0) }, FranjaZona() with { Hasta = new(8, 0) } })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"{url}/{e.Zona.Id}/franjas", solicitud)).StatusCode);
        using var horarioMalFormado = new StringContent("{\"nombre\":\"Inválida\",\"desde\":\"25:00:00\",\"hasta\":\"26:00:00\"}",
            System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync($"{url}/{e.Zona.Id}/franjas", horarioMalFormado)).StatusCode);
        await using var db = Context();
        Assert.Single(await db.Set<ZonaCobertura>().ToListAsync());
        Assert.Single(await db.Set<FranjaHoraria>().ToListAsync());
    }

    [PostgresFact]
    public async Task AdministracionZonasAislaOperadoresYRevalidaIdentidadAunqueLosClaimsDiganAdministrador()
    {
        var e = await PrepararAdministracionFlota();
        var otro = new EscenarioPersistencia();
        await using (var db = Context(otro.Operador.Id)) { db.Add(otro.Envio); await db.SaveChangesAsync(); }
        var propio = new AdministracionZonas(Options(), TenantFlota(e));
        var ajeno = new AdministracionZonas(Options(), TenantFlota(otro));
        Assert.Equal(e.Zona.Id, Assert.Single(await propio.ZonasAsync()).Id);
        Assert.Equal(otro.Zona.Id, Assert.Single(await ajeno.ZonasAsync()).Id);
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.FranjasAsync(otro.Zona.Id));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.CrearFranjaAsync(otro.Zona.Id, FranjaZona()));
        await Assert.ThrowsAsync<ArgumentException>(() => propio.FranjasAsync(Guid.Empty));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.FranjasAsync(Guid.NewGuid()));
        using var servidor = new ServidorApi(_testConnection); using var admin = servidor.Cliente(e, e.Admin);
        await Antiforgery(admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/administracion/zonas/{otro.Zona.Id}/franjas")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/administracion/zonas/{otro.Zona.Id}/franjas", FranjaZona())).StatusCode);
        foreach (var tenant in new[] { TenantActual.ParaOperador(e.Operador.Id),
            new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Repartidor.Id, RolUsuario.Administrador)),
            new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Despachador)) })
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => new AdministracionZonas(Options(), tenant).ZonasAsync());
        await using (var db = Context())
        { (await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id)).Desactivar(); await db.SaveChangesAsync(); }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.ZonasAsync());
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.CrearZonaAsync(SolicitudZona()));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => propio.CrearFranjaAsync(e.Zona.Id, FranjaZona()));
    }

    [PostgresFact]
    public async Task AdministracionZonasSeIntegraConTarifarioCotizacionYAltaSinCambiarEnviosAnteriores()
    {
        var e = await PrepararApiComercios();
        var gestion = new AdministracionZonas(Options(), TenantFlota(e));
        var zona = await gestion.CrearZonaAsync(SolicitudZona());
        var noche = await gestion.CrearFranjaAsync(zona.Id, new("Noche", new(19, 0), new(22, 0)));
        Assert.Contains(await Gestion(e).ListarZonasAsync(), z => z.Id == zona.Id);
        // Crear una zona no inventa su precio: se publica una nueva versión de tarifario con su línea.
        await Gestion(e).CrearTarifarioAsync(new(1, null, [NuevaLinea(e.Zona.Id), NuevaLinea(zona.Id)]));
        using var servidor = ServidorComercios(new()); using var cliente = servidor.Cliente();
        await LoginApiComercio(cliente);
        var opciones = (await cliente.GetFromJsonAsync<OpcionesComercio>("/api/comercios/envios/opciones", JsonApi))!;
        Assert.Contains(opciones.Zonas, z => z.Id == zona.Id && z.Franjas.Any(f => f.Id == noche.Id));
        var solicitud = SolicitudApiComercio(zona.Id);
        using var respuesta = await cliente.PostAsJsonAsync("/api/comercios/envios/cotizacion", solicitud, JsonApi);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cotizacion = (await respuesta.Content.ReadFromJsonAsync<CotizacionAlta>(JsonApi))!;
        var entrega = cotizacion.Opciones.First(o => o.FranjaId == noche.Id);
        using var alta = await cliente.PostAsJsonAsync("/api/comercios/envios", new AltaComercio("ZONA-NUEVA", solicitud, entrega.Clave), JsonApi);
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        await using var db = Context();
        var anterior = await db.Envios.Include(x => x.Programacion).ThenInclude(p => p.Franja).SingleAsync(x => x.Id == e.Envio.Id);
        Assert.Equal(e.Envio.Programacion.FranjaId, anterior.Programacion.FranjaId);
        Assert.Equal(e.Envio.VentanaDesde, anterior.VentanaDesde); Assert.Equal(e.Envio.VentanaHasta, anterior.VentanaHasta);
        var nuevo = await db.Envios.Include(x => x.Programacion).ThenInclude(p => p.Franja).Include(x => x.Zona)
            .SingleAsync(x => x.Id != e.Envio.Id);
        Assert.Equal(zona.Id, nuevo.ZonaId); Assert.Equal(noche.Id, nuevo.Programacion.FranjaId);
        Assert.Equal(entrega.Desde.UtcDateTime, nuevo.VentanaDesde); Assert.Equal(entrega.Hasta.UtcDateTime, nuevo.VentanaHasta);
    }

    [PostgresFact]
    public async Task AdministracionZonasAltasConcurrentesDeFranjasNoPierdenLasAnteriores()
    {
        var e = await PrepararAdministracionFlota();
        var opciones = new DbContextOptionsBuilder<AppDbContext>(Options()).AddInterceptors(new BarreraAltaFlota()).Options;
        var gestion = new AdministracionZonas(opciones, TenantFlota(e));
        var nuevas = await Task.WhenAll(gestion.CrearFranjaAsync(e.Zona.Id, FranjaZona("Primera")),
            gestion.CrearFranjaAsync(e.Zona.Id, FranjaZona("Segunda")));
        var franjas = await gestion.FranjasAsync(e.Zona.Id);
        Assert.Equal(3, franjas.Count);
        Assert.All(nuevas, f => Assert.Contains(franjas, p => p.Id == f.Id));
        Assert.Contains(franjas, f => f.Id == e.Envio.Programacion.FranjaId);
    }

    [PostgresFact]
    public async Task AdministracionZonasFalloDeFranjaRevierteLaZonaCompletaYNoAfectaLasExistentes()
    {
        var e = await PrepararAdministracionFlota();
        var gestion = new AdministracionZonas(Options(), TenantFlota(e));
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"FranjasHorarias\" ADD CONSTRAINT \"FalloZona\" CHECK (false) NOT VALID");
        await Assert.ThrowsAsync<DbUpdateException>(() => gestion.CrearZonaAsync(SolicitudZona()));
        await Assert.ThrowsAsync<DbUpdateException>(() => gestion.CrearFranjaAsync(e.Zona.Id, FranjaZona()));
        Assert.Equal(e.Zona.Id, Assert.Single(await gestion.ZonasAsync()).Id);
        Assert.Single(await gestion.FranjasAsync(e.Zona.Id));
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"FranjasHorarias\" DROP CONSTRAINT \"FalloZona\"");
        Assert.Equal("Norte", (await gestion.CrearZonaAsync(SolicitudZona())).Nombre);
        Assert.Equal("Tarde", (await gestion.CrearFranjaAsync(e.Zona.Id, FranjaZona())).Nombre);
    }
}
