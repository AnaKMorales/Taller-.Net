using UltimaMilla.Application.Notificaciones;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Notificaciones;

namespace UltimaMilla.Application.Tests;

public sealed class MensajesDestinatarioTests
{
    private static readonly TimeSpan Uruguay = TimeSpan.FromHours(-3);
    private static readonly DatosAvisoDestinatario Datos = new("Express Montevideo", "contacto@express.uy", "099 111 222",
        "Marta", "0af9e314c09c46f2ab2c9d0fd92c2c42",
        new DateTimeOffset(2026, 11, 16, 9, 0, 0, Uruguay), new DateTimeOffset(2026, 11, 16, 13, 0, 0, Uruguay));

    [Fact]
    public void ElAvisoLlevaMarcaContactoEnlaceYVentanaEnHoraLocal()
    {
        var aviso = MensajesDestinatario.Componer(EstadoEnvio.AsignadoARuta, EstadoEnvio.EnTransito, Datos,
            "marta@example.com", "https://ultimamilla.example/")!;

        Assert.Equal((CanalNotificacion.Email, "marta@example.com"), (aviso.Canal, aviso.Destino));
        Assert.Equal("Express Montevideo: tu envío está en camino", aviso.Asunto);
        Assert.Contains("Hola Marta: tu envío salió a reparto. Llega el 16/11 entre las 09:00 y las 13:00.", aviso.Cuerpo);
        Assert.Contains("Seguimiento: https://ultimamilla.example/seguimiento/0af9e314c09c46f2ab2c9d0fd92c2c42", aviso.Cuerpo);
        Assert.Contains("Consultas a Express Montevideo: contacto@express.uy · 099 111 222", aviso.Cuerpo);
    }

    [Theory]
    [InlineData(EstadoEnvio.EnDeposito)]
    [InlineData(EstadoEnvio.AsignadoARuta)]
    [InlineData(EstadoEnvio.Devuelto)]
    public void LosEstadosInternosNoSeAvisan(EstadoEnvio estado) =>
        Assert.Null(MensajesDestinatario.Componer(EstadoEnvio.Admitido, estado, Datos, "m@example.com", null));

    [Fact]
    public void SinCambioRealDeEstadoNoSeAvisa() =>
        Assert.Null(MensajesDestinatario.Componer(EstadoEnvio.EntregadoParcialmente, EstadoEnvio.EntregadoParcialmente,
            Datos, "m@example.com", null));

    [Fact]
    public void TodosLosEstadosRelevantesTienenTexto()
    {
        foreach (var estado in MensajesDestinatario.Relevantes)
        {
            var aviso = MensajesDestinatario.Componer(null, estado, Datos, "m@example.com", null);
            Assert.NotNull(aviso);
            Assert.StartsWith("Express Montevideo: ", aviso.Asunto);
        }
    }

    [Fact]
    public void SinUrlPublicaQuedaLaRutaYSinContactoNoHayLineaDeConsultas()
    {
        var aviso = MensajesDestinatario.Componer(null, EstadoEnvio.Entregado, Datos with { EmailContacto = null, TelefonoContacto = " " },
            "m@example.com", null)!;
        Assert.Contains("Seguimiento: /seguimiento/0af9e314c09c46f2ab2c9d0fd92c2c42", aviso.Cuerpo);
        Assert.DoesNotContain("Consultas", aviso.Cuerpo);
    }
}
