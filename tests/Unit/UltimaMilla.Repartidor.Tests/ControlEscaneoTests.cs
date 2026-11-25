using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Dispositivo;

namespace UltimaMilla.Repartidor.Tests;

public sealed class ControlEscaneoTests
{
    private static BultoParadaDto[] Bultos() =>
    [
        new(Guid.NewGuid(), "B001", "EnTransito", 1, 0.01m),
        new(Guid.NewGuid(), "B002", "EnTransito", 1, 0.01m),
        new(Guid.NewGuid(), "B003", "EnTransito", 1, 0.01m),
    ];

    [Fact]
    public void AvisaFaltantesYSobrantesMientrasSeEscanea()
    {
        var control = new ControlEscaneo(Bultos());
        Assert.False(control.HuboLecturas);

        Assert.Equal(ResultadoLectura.Nuevo, control.Leer(" B002\n").Resultado);
        Assert.Equal(ResultadoLectura.Sobrante, control.Leer("X999").Resultado);
        Assert.Equal(ResultadoLectura.Sobrante, control.Leer("X999").Resultado); // repetido: no se duplica

        Assert.True(control.HuboLecturas);
        Assert.Equal(["B001", "B003"], control.Faltantes.Select(b => b.Codigo));
        Assert.Equal(["X999"], control.Sobrantes);
        Assert.False(control.Completo);
    }

    [Fact]
    public void UnBultoLeidoDosVecesCuentaUnaSolaYAlFinalNoFaltaNada()
    {
        var control = new ControlEscaneo(Bultos());

        Assert.Equal(ResultadoLectura.Nuevo, control.Leer("B001").Resultado);
        Assert.Equal(ResultadoLectura.Repetido, control.Leer("B001").Resultado);
        control.Leer("B002");
        control.Leer("B003");

        Assert.Equal(3, control.Escaneados.Count);
        Assert.Empty(control.Faltantes);
        Assert.Empty(control.Sobrantes);
        Assert.True(control.Completo);
        Assert.Equal(ResultadoLectura.Vacio, control.Leer("  ").Resultado);
    }
}
