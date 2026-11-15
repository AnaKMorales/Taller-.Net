using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private static Task<HttpResponseMessage> FormularioCierre(HttpClient cliente, string ruta, Guid version, string? codigo = null) =>
        cliente.PostAsync(ruta, new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Version"] = version.ToString(), ["Codigo"] = codigo ?? "" }));

    [PostgresFact]
    public async Task PantallasCierreRecibenDevolucionYRindenConCsrfYVersion()
    {
        var (e, rutaId) = await PrepararCierreDeposito();
        using var servidor = new ServidorApi(_testConnection);
        ((RelojConfiguracion)servidor.Services.GetRequiredService<TimeProvider>()).Ahora = EscenarioPersistencia.Fecha.AddHours(3);
        using var admin = servidor.Cliente(e, e.Admin);
        var url = $"/backoffice/deposito/devolucion/{e.Envio.Id}";
        var ruta = $"/backoffice/deposito/rendicion/{rutaId}";
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
        var estado = await HandlerDevolucion(e).ConsultarAsync(e.Envio.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await FormularioCierre(admin, url + "?handler=Iniciar", estado.Version)).StatusCode);
        await Antiforgery(admin);
        Assert.Equal(HttpStatusCode.Redirect, (await FormularioCierre(admin, url + "?handler=Iniciar", estado.Version)).StatusCode);
        Assert.Contains(e.Envio.CodigoSeguimiento, await admin.GetStringAsync("/backoffice/deposito/cierre"));
        estado = await HandlerDevolucion(e).ConsultarAsync(e.Envio.Id);
        // Una versión obsoleta se presenta como error sin completar la devolución.
        Assert.Equal(HttpStatusCode.OK, (await FormularioCierre(admin, url + "?handler=Completar", Guid.NewGuid())).StatusCode);
        Assert.Equal(EstadoDevolucion.EnProceso, (await HandlerDevolucion(e).ConsultarAsync(e.Envio.Id)).Devolucion!.Estado);
        foreach (var b in estado.Devolucion!.Bultos)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await FormularioCierre(admin, url + "?handler=Escanear", estado.Version, b.Codigo)).StatusCode);
            estado = await HandlerDevolucion(e).ConsultarAsync(e.Envio.Id);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await FormularioCierre(admin, url + "?handler=Completar", estado.Version)).StatusCode);
        Assert.Contains("Completada", await admin.GetStringAsync(url));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(ruta)).StatusCode);
        var antes = await HandlerRendicion(e).ConsultarAsync(rutaId);
        Assert.Equal(HttpStatusCode.Redirect, (await FormularioCierre(admin, ruta + "?handler=Rendir", antes.Version)).StatusCode);
        Assert.NotNull((await HandlerRendicion(e).ConsultarAsync(rutaId)).RendidaEn);
    }

    [PostgresFact]
    public async Task PantallasCierreAislanOperadoresYNoRindenParadasActivas()
    {
        var (e, rutaId) = await PrepararCierreDeposito(intento: false);
        var ajeno = Escenario();
        await using (var db = Context(ajeno.Operador.Id)) { db.Add(ajeno.Admin); await db.SaveChangesAsync(); }
        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin);
        using var otro = servidor.Cliente(ajeno, ajeno.Admin);
        using var repartidor = servidor.Cliente(e);
        var url = $"/backoffice/deposito/rendicion/{rutaId}";
        Assert.Equal(HttpStatusCode.NotFound, (await otro.GetAsync(url)).StatusCode);
        Assert.False((await repartidor.GetAsync("/backoffice/deposito/cierre")).IsSuccessStatusCode);
        await Antiforgery(admin);
        var antes = await HandlerRendicion(e).ConsultarAsync(rutaId);
        Assert.DoesNotContain("Confirmar rendición", await admin.GetStringAsync(url));
        Assert.Equal(HttpStatusCode.OK, (await FormularioCierre(admin, url + "?handler=Rendir", antes.Version)).StatusCode);
        Assert.Null((await HandlerRendicion(e).ConsultarAsync(rutaId)).RendidaEn);
    }
}
