using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Dispositivo;

namespace UltimaMilla.Repartidor.Tests;

public sealed class LecturaCodigoTests
{
    private static readonly BultoParadaDto[] Bultos =
    [
        new(Guid.NewGuid(), "B001", "EnTransito", 1, 0.01m),
        new(Guid.NewGuid(), "B002", "EnTransito", 1, 0.01m)
    ];

    [Theory]
    [InlineData("B002")]
    [InlineData("  B002 ")]
    [InlineData("B002\n")]
    public void EncuentraElBultoAunqueElLectorAgregueEspacios(string leido) =>
        Assert.Same(Bultos[1], LecturaCodigo.Buscar(Bultos, leido));

    [Theory]
    [InlineData("b002")]
    [InlineData("B003")]
    [InlineData("")]
    [InlineData(null)]
    public void UnCodigoAjenoOVacioNoMarcaNada(string? leido) =>
        Assert.Null(LecturaCodigo.Buscar(Bultos, leido));

    [Theory]
    [InlineData("permiso", "permiso")]
    [InlineData("no-disponible", "navegador")]
    [InlineData("camara", "abrir la cámara")]
    [InlineData(null, "abrir la cámara")]
    public void SinCamaraExplicaQueHacer(string? motivo, string contiene) =>
        Assert.Contains(contiene, LecturaCodigo.MotivoSinCamara(motivo));
}
