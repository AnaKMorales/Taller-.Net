using UltimaMilla.Repartidor.Dispositivo;

namespace UltimaMilla.Repartidor.Tests;

// Qué estado y qué mensaje ve el repartidor según lo que devolvió el GPS del navegador.
public sealed class LecturaUbicacionTests
{
    [Fact]
    public void ConPosicionNoMuestraMensaje()
    {
        var lectura = LecturaUbicacion.DesdeNavegador([0, -34.9011234567, -56.1645]);

        Assert.Equal(EstadoUbicacion.Disponible, lectura.Estado);
        Assert.Equal(-34.901123m, lectura.Posicion!.Latitud);
        Assert.Equal(-56.1645m, lectura.Posicion.Longitud);
        Assert.Null(lectura.Mensaje);
        Assert.False(lectura.PermiteReintentar);
    }

    [Fact]
    public void PermisoDenegadoPideHabilitarloYPermiteReintentar()
    {
        var lectura = LecturaUbicacion.DesdeNavegador([1]);

        Assert.Equal(EstadoUbicacion.PermisoDenegado, lectura.Estado);
        Assert.Null(lectura.Posicion);
        Assert.Contains("bloqueada", lectura.Mensaje);
        Assert.Contains("permisos del sitio", lectura.Mensaje);
        Assert.True(lectura.PermiteReintentar);
    }

    [Theory]
    [InlineData(2)] // posición no disponible
    [InlineData(3)] // tiempo agotado
    public void SinSenalOTiempoAgotadoSugiereReintentar(int codigo)
    {
        var lectura = LecturaUbicacion.DesdeNavegador([codigo]);

        Assert.Equal(EstadoUbicacion.SinSenal, lectura.Estado);
        Assert.Contains("No hay señal de GPS", lectura.Mensaje);
        Assert.DoesNotContain("permisos", lectura.Mensaje);
        Assert.True(lectura.PermiteReintentar);
    }

    [Fact]
    public void SinSoporteNoOfreceReintentar()
    {
        var lectura = LecturaUbicacion.DesdeNavegador([-1]);

        Assert.Equal(EstadoUbicacion.NoSoportada, lectura.Estado);
        Assert.Contains("no permite obtener la ubicación", lectura.Mensaje);
        Assert.False(lectura.PermiteReintentar);
    }

    [Fact]
    public void RespuestaInesperadaSeTrataComoSinSenal()
    {
        Assert.Equal(EstadoUbicacion.SinSenal, LecturaUbicacion.DesdeNavegador(null).Estado);
        Assert.Equal(EstadoUbicacion.SinSenal, LecturaUbicacion.DesdeNavegador([]).Estado);
        Assert.Equal(EstadoUbicacion.SinSenal, LecturaUbicacion.DesdeNavegador([0]).Estado);
    }

    [Fact]
    public void DenegadoYSinSenalTienenMensajesDistintos() =>
        Assert.NotEqual(LecturaUbicacion.DesdeNavegador([1]).Mensaje, LecturaUbicacion.DesdeNavegador([2]).Mensaje);
}
