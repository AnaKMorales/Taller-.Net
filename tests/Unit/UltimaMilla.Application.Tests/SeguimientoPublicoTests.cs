using System.Text.Json;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tests;

public sealed class SeguimientoPublicoTests
{
    [Fact]
    public void LaVistaRecuperadaDeCacheConservaIgualdadPorContenido()
    {
        var vista = CrearVista();
        var recuperada = JsonSerializer.Deserialize<SeguimientoPublicoDto>(JsonSerializer.Serialize(vista))!;

        Assert.NotSame(vista.Historial, recuperada.Historial);
        Assert.Equal(vista, recuperada);
        Assert.Equal(vista.GetHashCode(), recuperada.GetHashCode());
    }

    [Fact]
    public void LosCambiosDeAvanceEHistorialDistinguenVersionesDeLaVista()
    {
        var vista = CrearVista();

        Assert.NotEqual(vista, vista with { Bultos = new(3, 2, 0) });
        Assert.NotEqual(vista, vista with { Historial = vista.Historial.Reverse().ToArray() });
    }

    private static SeguimientoPublicoDto CrearVista()
    {
        var inicio = new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);
        return new("Operador", EstadoEnvio.EntregadoParcialmente, inicio.UtcDateTime,
            inicio, inicio.AddHours(2), new(3, 1, 0),
            [new(TipoEvento.Admitir, EstadoEnvio.Admitido, inicio.UtcDateTime),
             new(TipoEvento.EntregaParcial, EstadoEnvio.EntregadoParcialmente, inicio.AddMinutes(30).UtcDateTime)]);
    }
}
