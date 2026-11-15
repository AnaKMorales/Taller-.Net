using System.Net;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Envios.Extravio;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private static FormUrlEncodedContent FormularioExtravio(ConsultaExtravioDto estado, bool confirmar = true, string detalle = "Bulto no localizado en vehículo") =>
        new(new Dictionary<string, string>
        {
            ["VersionExtravioEnvio"] = estado.VersionEnvio.ToString(),
            ["VersionExtravioRuta"] = estado.VersionRuta?.ToString() ?? "",
            ["DetalleExtravio"] = detalle, ["ConfirmarExtravio"] = confirmar.ToString()
        });

    [PostgresFact]
    public async Task PantallaExtravioValidaCsrfConfirmacionVersionYFinalizaParada()
    {
        var (e, rutaId) = await PrepararCierreDeposito(intento: false);
        using var servidor = new ServidorApi(_testConnection);
        ((RelojConfiguracion)servidor.Services.GetRequiredService<TimeProvider>()).Ahora = EscenarioPersistencia.Fecha.AddHours(3);
        using var admin = servidor.Cliente(e, e.Admin);
        var url = $"/backoffice/envios/{e.Envio.Id}";
        Assert.Contains("Confirmar extrav", await admin.GetStringAsync(url));
        var estado = await Extravios(e).ConsultarAsync(e.Envio.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync(url + "?handler=Extraviar", FormularioExtravio(estado))).StatusCode);
        await Antiforgery(admin);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync(url + "?handler=Extraviar", FormularioExtravio(estado, false))).StatusCode);
        Assert.Equal(EstadoEnvio.EnTransito, (await Extravios(e).ConsultarAsync(e.Envio.Id)).Estado);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync(url + "?handler=Extraviar", FormularioExtravio(estado with { VersionRuta = Guid.NewGuid() }))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync(url + "?handler=Extraviar", FormularioExtravio(estado, detalle: " "))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync(url + "?handler=Extraviar", FormularioExtravio(estado))).StatusCode);
        Assert.DoesNotContain("Confirmar extrav", await admin.GetStringAsync(url));
        var final = await Extravios(e).ConsultarAsync(e.Envio.Id);
        Assert.Equal(EstadoEnvio.Extraviado, final.Estado);
        Assert.Equal(0, (await HandlerRendicion(e).ConsultarAsync(rutaId)).ParadasPendientes);
    }

    [PostgresFact]
    public async Task PantallaExtravioNoSeOfreceAlOperarioNiAlEnvioAdmitido()
    {
        var e = await PrepararAdministracionFlota();
        Usuario operario;
        await using (var db = Context())
        {
            operario = new Usuario(await db.Set<Operador>().SingleAsync(), "deposito-extravio@example.com", RolUsuario.OperarioDeposito);
            db.Add(operario); await db.SaveChangesAsync();
        }
        using var servidor = new ServidorApi(_testConnection);
        using var deposito = servidor.Cliente(e, operario);
        using var admin = servidor.Cliente(e, e.Admin);
        var url = $"/backoffice/envios/{e.Envio.Id}";
        Assert.DoesNotContain("Confirmar extrav", await admin.GetStringAsync(url));
        Assert.DoesNotContain("Confirmar extrav", await deposito.GetStringAsync(url));
        await Antiforgery(deposito);
        var estado = await Extravios(e).ConsultarAsync(e.Envio.Id);
        Assert.False((await deposito.PostAsync(url + "?handler=Extraviar", FormularioExtravio(estado))).IsSuccessStatusCode);
        Assert.Equal(EstadoEnvio.Admitido, (await Extravios(e).ConsultarAsync(e.Envio.Id)).Estado);
    }
}
