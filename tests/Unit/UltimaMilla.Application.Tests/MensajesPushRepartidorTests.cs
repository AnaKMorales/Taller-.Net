using UltimaMilla.Application.Notificaciones;

namespace UltimaMilla.Application.Tests;

public sealed class MensajesPushRepartidorTests
{
    [Theory]
    [InlineData("Asignar", true)]
    [InlineData("Reasignar", true)]
    [InlineData("IniciarEntrega", false)]
    [InlineData("Admitir", false)]
    public void SoloLasAsignacionesSonRelevantesParaElRepartidor(string tipo, bool relevante) =>
        Assert.Equal(relevante, MensajesPushRepartidor.EsRelevante(tipo));

    [Fact]
    public void LaReasignacionDiceQueSeSumoALaRutaConCodigoCortoYParada()
    {
        var envio = Guid.NewGuid();
        var llegada = new DateTime(2026, 11, 24, 15, 30, 0, DateTimeKind.Unspecified);

        var aviso = MensajesPushRepartidor.Componer("Reasignar", envio, "abcdef0123456789abcdef0123456789", 3, llegada);

        Assert.Equal("Envío reasignado a tu ruta", aviso.Titulo);
        Assert.Equal("Envío ABCDEF01 · parada 3. Abrí la app para ver la hoja de ruta.", aviso.Cuerpo);
        Assert.Equal($"envio-{envio:N}", aviso.Etiqueta);
        Assert.Equal(DateTimeKind.Utc, aviso.LlegadaEstimada!.Value.Kind);
        Assert.Equal("Nuevo envío en tu ruta", MensajesPushRepartidor.Componer("Asignar", envio, "X1", 1, llegada).Titulo);
    }
}
