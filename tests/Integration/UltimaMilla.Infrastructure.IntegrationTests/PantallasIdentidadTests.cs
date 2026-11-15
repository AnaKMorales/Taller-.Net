using System.Net;
using System.Net.Http.Json;
using UltimaMilla.Application.Administracion;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private static FormUrlEncodedContent FormularioIdentidad(Guid version, string marca = "Marca portal") => new(new Dictionary<string, string>
    {
        ["Version"] = version.ToString(), ["Marca"] = marca, ["Primario"] = "#16324F",
        ["Secundario"] = "#E4F0FA", ["Telefono"] = "099123456", ["Email"] = "marca@example.com",
        ["Logo"] = "https://marca.example/logo.png"
    });

    [PostgresFact]
    public async Task PantallasIdentidadGuardanMarcaYLaAplicanEnPortalYSeguimientoSinFiltrarOtroOperador()
    {
        var e = await PrepararApiComercios();
        var otro = Escenario();
        otro.Envio.CuentaComercial.Usuarios.Single().ActualizarEmail("otro-comercio@example.com");
        await using (var db = Context(otro.Operador.Id)) { db.Add(otro.Envio); await db.SaveChangesAsync(); }
        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin);
        using var publica = servidor.Cliente();
        const string url = "/backoffice/configuracion/identidad";
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
        var previo = (await admin.GetFromJsonAsync<EstadoIdentidadOperador>("/api/administracion/identidad"))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync(url, FormularioIdentidad(previo.Version))).StatusCode);
        await Antiforgery(admin);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync(url, FormularioIdentidad(previo.Version))).StatusCode);
        // El formulario viejo no puede sobrescribir un cambio ya confirmado.
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync(url, FormularioIdentidad(previo.Version, "No debe guardarse"))).StatusCode);
        using var seguimiento = await publica.GetAsync($"/seguimiento/{e.Envio.CodigoSeguimiento}");
        seguimiento.EnsureSuccessStatusCode();
        var html = await seguimiento.Content.ReadAsStringAsync();
        Assert.Contains("Marca portal", html); Assert.Contains("marca@example.com", html);
        Assert.Contains("background-color:#16324F", html); Assert.Contains("background-color:#E4F0FA", html);
        Assert.Contains("https://marca.example/logo.png", html);
        Assert.Contains("img-src 'self' https:", seguimiento.Headers.GetValues("Content-Security-Policy").Single());
        Assert.DoesNotContain("Marca portal", await publica.GetStringAsync($"/seguimiento/{otro.Envio.CodigoSeguimiento}"));
        using var real = ServidorComercios(new()); using var comercio = real.Cliente();
        await LoginApiComercio(comercio);
        var portal = await comercio.GetStringAsync("/portal");
        Assert.Contains("Marca portal", portal); Assert.Contains("marca@example.com", portal);
        Assert.Contains("background-color:#16324F", portal); Assert.Contains("https://marca.example/logo.png", portal);
        Assert.DoesNotContain("No debe guardarse", portal);
    }

    [PostgresFact]
    public async Task PantallasIdentidadRestringenEdicionYRechazanDatosInvalidos()
    {
        var e = await PrepararApiComercios();
        using var servidor = new ServidorApi(_testConnection);
        using var repartidor = servidor.Cliente(e);
        using var admin = servidor.Cliente(e, e.Admin);
        const string url = "/backoffice/configuracion/identidad";
        Assert.False((await repartidor.GetAsync(url)).IsSuccessStatusCode);
        await Antiforgery(repartidor); await Antiforgery(admin);
        var previo = (await admin.GetFromJsonAsync<EstadoIdentidadOperador>("/api/administracion/identidad"))!;
        Assert.False((await repartidor.PostAsync(url, FormularioIdentidad(previo.Version))).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync(url, FormularioIdentidad(previo.Version, " "))).StatusCode);
        var actual = (await admin.GetFromJsonAsync<EstadoIdentidadOperador>("/api/administracion/identidad"))!;
        Assert.Equal(previo, actual);
    }
}
