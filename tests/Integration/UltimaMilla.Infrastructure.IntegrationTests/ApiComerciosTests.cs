using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Envios.Importacion;
using UltimaMilla.Infrastructure.Persistence.Outbox;
using UltimaMilla.Infrastructure.Persistence.Tenancy;
using UltimaMilla.Web.Api;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private const string ClaveApiComercio = "Comercio-prueba-123!";
    private async Task<EscenarioPersistencia> PrepararApiComercios()
    {
        var e = Escenario();
        await using var db = Context();
        db.Add(e.Envio); await db.SaveChangesAsync();
        await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, e.Envio.CuentaComercial.Usuarios.Single(), ClaveApiComercio);
        await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, e.Admin, ClaveApiComercio);
        await db.SaveChangesAsync();
        return e;
    }
    private ServidorApi ServidorComercios(RelojConfiguracion reloj) => new(_testConnection, usarIdentidadPrueba: false,
        configurarServicios: services => { services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(reloj); });
    private static async Task LoginApiComercio(HttpClient cliente, string email = "usuario@example.com")
    {
        using var respuesta = await cliente.PostAsJsonAsync("/api/comercios/sesion", new LoginComercio(email, ClaveApiComercio));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var token = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
    }
    private static SolicitudAltaPortal SolicitudApiComercio(Guid zona) => new(zona, ModalidadServicio.Estandar,
        "Persona", "099123456", "p@example.com", "Calle", "1", "Montevideo", [new BultoAlta("CAJA", 1, 10, 10, 10)]);
    private static async Task<AltaComercio> CotizarApiComercio(HttpClient cliente, Guid zona, string referencia = "PEDIDO-API-1")
    {
        var solicitud = SolicitudApiComercio(zona);
        using var respuesta = await cliente.PostAsJsonAsync("/api/comercios/envios/cotizacion", solicitud, JsonApi);
        respuesta.EnsureSuccessStatusCode();
        var cotizacion = (await respuesta.Content.ReadFromJsonAsync<CotizacionAlta>(JsonApi))!;
        return new(referencia, solicitud, cotizacion.Opciones[0].Clave);
    }

    [PostgresFact]
    public async Task ApiComerciosAltaCalculaTarifaYReenvioComparteIdempotenciaConImportacion()
    {
        var e = await PrepararApiComercios();
        var reloj = new RelojConfiguracion { Ahora = EscenarioPersistencia.Fecha };
        using var servidor = ServidorComercios(reloj);
        using var cliente = servidor.Cliente();
        await LoginApiComercio(cliente);
        var opciones = (await cliente.GetFromJsonAsync<OpcionesComercio>("/api/comercios/envios/opciones", JsonApi))!;
        Assert.Equal(e.Zona.Id, Assert.Single(opciones.Zonas).Id);
        var solicitud = await CotizarApiComercio(cliente, e.Zona.Id);
        // Campos extra no pueden elegir otra cuenta, operador o precio.
        using var respuesta = await cliente.PostAsJsonAsync("/api/comercios/envios", new {
            solicitud.Referencia, solicitud.Solicitud, solicitud.ClaveEntrega,
            operadorId = Guid.NewGuid(), cuentaComercialId = Guid.NewGuid(), tarifa = 0.01m }, JsonApi);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var creada = (await respuesta.Content.ReadFromJsonAsync<ResultadoAltaComercio>(JsonApi))!;
        Assert.False(creada.Repetido);
        Assert.Equal(165, creada.Envio.Tarifa);
        reloj.Ahora = reloj.Ahora.AddDays(20);
        await LoginApiComercio(cliente); // La respuesta histórica sobrevive a una nueva sesión; el token anterior expiró.
        using var repetida = await cliente.PostAsJsonAsync("/api/comercios/envios", solicitud, JsonApi);
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        var anterior = (await repetida.Content.ReadFromJsonAsync<ResultadoAltaComercio>(JsonApi))!;
        Assert.True(anterior.Repetido);
        Assert.Equal(creada.Envio, anterior.Envio);
        Assert.Equal(HttpStatusCode.Conflict, (await cliente.PostAsJsonAsync("/api/comercios/envios",
            solicitud with { Solicitud = solicitud.Solicitud with { Calle = "Otra" } }, JsonApi)).StatusCode);
        var importada = Assert.Single(await Importador(e).Handle([new(solicitud.Referencia, solicitud.Solicitud, solicitud.ClaveEntrega)]));
        Assert.Equal(EstadoFilaImportacion.Existente, importada.Estado);
        Assert.Equal(creada.Envio, importada.Envio);
        await using var lectura = Context();
        var envio = await lectura.Envios.Include(x => x.CuentaComercial).SingleAsync(x => x.CodigoSeguimiento == creada.Envio.CodigoSeguimiento);
        Assert.Equal(e.Envio.CuentaComercial.Id, envio.CuentaComercialId);
        Assert.Equal(2, await lectura.Envios.CountAsync());
        Assert.Equal(2, await lectura.Set<MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task ApiComerciosExigeBearerYRolComercioYRevalidaUsuarioRevocado()
    {
        var e = await PrepararApiComercios();
        using var servidor = ServidorComercios(new() { Ahora = EscenarioPersistencia.Fecha });
        using var cliente = servidor.Cliente();
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/comercios/envios/opciones")).StatusCode);
        using var conCookie = servidor.Cliente();
        var claims = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, e.Envio.CuentaComercial.Usuarios.Single().Id.ToString()),
            new Claim(ClaimTypes.Role, nameof(RolUsuario.Comercio)),
            new Claim(ClaimsUltimaMilla.Operador, e.Operador.Id.ToString()),
            new Claim(ClaimsUltimaMilla.CuentaComercial, e.Envio.CuentaComercial.Id.ToString())], CookieAuthenticationDefaults.AuthenticationScheme);
        var cookie = servidor.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme).TicketDataFormat.Protect(new AuthenticationTicket(
                new ClaimsPrincipal(claims), new AuthenticationProperties {
                    IssuedUtc = new DateTimeOffset(EscenarioPersistencia.Fecha),
                    ExpiresUtc = new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(8)) }, CookieAuthenticationDefaults.AuthenticationScheme));
        conCookie.DefaultRequestHeaders.Add("Cookie", "ultimamilla.sesion=" + cookie);
        Assert.Equal(HttpStatusCode.OK, (await conCookie.GetAsync("/api/antiforgery")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await conCookie.GetAsync("/api/comercios/envios/opciones")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/comercios/sesion", new LoginComercio(e.Admin.Email, ClaveApiComercio))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/comercios/sesion", new LoginComercio("usuario@example.com", "incorrecta"))).StatusCode);
        await AsignarContrasenaIntentosAsync(e.Operador.Id, e.Repartidor.Id);
        using var repartidor = servidor.Cliente();
        await IngresarIntentosAsync(repartidor, e.Repartidor.Email);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync("/api/comercios/envios/opciones")).StatusCode);
        await LoginApiComercio(cliente);
        var solicitud = await CotizarApiComercio(cliente, e.Zona.Id);
        await using (var db = Context())
        { (await db.Set<Usuario>().SingleAsync(u => u.Id == e.Envio.CuentaComercial.Usuarios.Single().Id)).Desactivar(); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/comercios/envios/opciones")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync("/api/comercios/envios", solicitud, JsonApi)).StatusCode);
        await using var lectura = Context();
        Assert.Single(await lectura.Envios.ToListAsync());
    }

    [PostgresFact]
    public async Task ApiComerciosReferenciaSeAislaPorCuentaYNoCotizaZonasAjenas()
    {
        var e = await PrepararApiComercios();
        CuentaComercial otraCuenta;
        Usuario usuario;
        await using (var db = Context())
        {
            var operador = await db.Set<Operador>().SingleAsync();
            otraCuenta = new(operador, new Comercio("Otro", "099000007", "otro@example.com"));
            usuario = new(operador, "integracion-otra@example.com", RolUsuario.Comercio);
            usuario.AsignarCuentaComercial(otraCuenta);
            db.Add(otraCuenta); await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, usuario, ClaveApiComercio); await db.SaveChangesAsync();
        }
        using var servidor = ServidorComercios(new() { Ahora = EscenarioPersistencia.Fecha });
        using var primero = servidor.Cliente(); using var segundo = servidor.Cliente();
        await LoginApiComercio(primero); await LoginApiComercio(segundo, usuario.Email);
        var solicitud = await CotizarApiComercio(primero, e.Zona.Id);
        using var respuestaA = await primero.PostAsJsonAsync("/api/comercios/envios", solicitud, JsonApi);
        using var respuestaB = await segundo.PostAsJsonAsync("/api/comercios/envios", solicitud, JsonApi);
        Assert.Equal(HttpStatusCode.Created, respuestaA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, respuestaB.StatusCode);
        var a = (await respuestaA.Content.ReadFromJsonAsync<ResultadoAltaComercio>(JsonApi))!;
        var b = (await respuestaB.Content.ReadFromJsonAsync<ResultadoAltaComercio>(JsonApi))!;
        Assert.NotEqual(a.Envio.CodigoSeguimiento, b.Envio.CodigoSeguimiento);
        Assert.Equal(HttpStatusCode.BadRequest, (await primero.PostAsJsonAsync("/api/comercios/envios/cotizacion",
            solicitud.Solicitud with { ZonaId = Guid.NewGuid() }, JsonApi)).StatusCode);
        await using var dbFinal = Context();
        Assert.Equal(3, await dbFinal.Envios.CountAsync());
        Assert.Equal(otraCuenta.Id, (await dbFinal.Envios.Include(x => x.CuentaComercial).SingleAsync(x => x.CodigoSeguimiento == b.Envio.CodigoSeguimiento)).CuentaComercialId);
    }

    [PostgresFact]
    public async Task ApiComerciosAltasConcurrentesCreanUnSoloEnvio()
    {
        var e = await PrepararApiComercios();
        using var servidor = ServidorComercios(new() { Ahora = EscenarioPersistencia.Fecha });
        using var cliente = servidor.Cliente(); await LoginApiComercio(cliente);
        var solicitud = await CotizarApiComercio(cliente, e.Zona.Id);
        var respuestas = await Task.WhenAll(cliente.PostAsJsonAsync("/api/comercios/envios", solicitud, JsonApi),
            cliente.PostAsJsonAsync("/api/comercios/envios", solicitud, JsonApi));
        try
        {
            Assert.Single(respuestas, x => x.StatusCode == HttpStatusCode.Created);
            Assert.Single(respuestas, x => x.StatusCode == HttpStatusCode.OK);
            await using var db = Context();
            Assert.Equal(2, await db.Envios.CountAsync());
            Assert.Equal(2, await db.Set<MensajeOutbox>().CountAsync());
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    [PostgresFact]
    public async Task ApiComerciosFalloDeRegistroRevierteElAltaYRechazaContenidoInvalido()
    {
        var e = await PrepararApiComercios();
        using var servidor = ServidorComercios(new() { Ahora = EscenarioPersistencia.Fecha });
        using var cliente = servidor.Cliente(); await LoginApiComercio(cliente);
        var solicitud = await CotizarApiComercio(cliente, e.Zona.Id);
        foreach (var invalida in new[] { solicitud with { Referencia = " " }, solicitud with { ClaveEntrega = "invalida" },
            solicitud with { Solicitud = solicitud.Solicitud with { Bultos = [] } } })
            Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/comercios/envios", invalida, JsonApi)).StatusCode);
        await using (var db = Context())
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"AltasImportadas\" ADD CONSTRAINT \"FalloApi\" CHECK (false) NOT VALID");
        Assert.Equal(HttpStatusCode.InternalServerError, (await cliente.PostAsJsonAsync("/api/comercios/envios", solicitud, JsonApi)).StatusCode);
        await using (var db = Context())
        {
            Assert.Single(await db.Envios.ToListAsync());
            Assert.Single(await db.Set<MensajeOutbox>().ToListAsync());
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"AltasImportadas\" DROP CONSTRAINT \"FalloApi\"");
        }
        Assert.Equal(HttpStatusCode.Created, (await cliente.PostAsJsonAsync("/api/comercios/envios", solicitud, JsonApi)).StatusCode);
    }

    [PostgresFact]
    public async Task ApiComerciosNoExponeNiAdmiteConfiguracionDeOtroOperador()
    {
        var e = await PrepararApiComercios();
        using var servidor = ServidorComercios(new() { Ahora = EscenarioPersistencia.Fecha });
        using var cliente = servidor.Cliente(); await LoginApiComercio(cliente);
        var otro = new EscenarioPersistencia();
        await using (var db = Context(otro.Operador.Id)) { db.Add(otro.Envio); await db.SaveChangesAsync(); }
        var opciones = (await cliente.GetFromJsonAsync<OpcionesComercio>("/api/comercios/envios/opciones", JsonApi))!;
        Assert.Equal(e.Zona.Id, Assert.Single(opciones.Zonas).Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/comercios/envios/cotizacion",
            SolicitudApiComercio(otro.Zona.Id), JsonApi)).StatusCode);
        var solicitud = await CotizarApiComercio(cliente, e.Zona.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync("/api/comercios/envios",
            solicitud with { Solicitud = solicitud.Solicitud with { ZonaId = otro.Zona.Id } }, JsonApi)).StatusCode);
        await using var lectura = Context();
        Assert.Single(await lectura.Envios.ToListAsync());
    }

    [PostgresFact]
    public async Task ApiComerciosLoginRechazaCuentaAmbiguaYLimitaIntentos()
    {
        var e = await PrepararApiComercios();
        await using (var db = Context())
        {
            var cuenta = new CuentaComercial(await db.Set<Operador>().SingleAsync(), new Comercio("Otra", "099000007", "otra@example.com"));
            (await db.Set<Usuario>().SingleAsync(u => u.Id == e.Envio.CuentaComercial.Usuarios.Single().Id)).AsignarCuentaComercial(cuenta);
            db.Add(cuenta); await db.SaveChangesAsync();
        }
        using var servidor = ServidorComercios(new() { Ahora = EscenarioPersistencia.Fecha });
        using var cliente = servidor.Cliente();
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/comercios/sesion", new LoginComercio("usuario@example.com", ClaveApiComercio))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await cliente.PostAsJsonAsync("/api/comercios/sesion", new LoginComercio("usuario@example.com", ClaveApiComercio))).StatusCode);
    }
}
