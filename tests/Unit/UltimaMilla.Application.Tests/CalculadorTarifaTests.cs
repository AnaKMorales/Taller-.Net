using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tests;

public sealed class CalculadorTarifaTests
{
    private static readonly Guid Zona = Guid.NewGuid();
    private static TarifarioDto Tarifario(params LineaTarifaDto[] lineas) => new(Guid.NewGuid(), 2,
        new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), null, lineas);
    private static LineaTarifaDto Linea(Guid? zona = null, ModalidadServicio modalidad = ModalidadServicio.Estandar,
        decimal precio = 100, decimal volumenHasta = 1) => new(zona ?? Zona, modalidad, 0, 10, volumenHasta, precio, []);

    [Fact]
    public void SumaBultosEnKgYM3YConservaLaVersionDelTarifario()
    {
        var tarifa = Tarifario(Linea(volumenHasta: 0.009m));
        BultoAlta[] bultos = [new("A", 1.5m, 10, 10, 10), new("B", 2, 20, 20, 20)];
        var result = new CalculadorTarifa().Calcular(tarifa, Zona, ModalidadServicio.Estandar, bultos);
        Assert.Equal(3.5m, result.PesoTotalKg);
        Assert.Equal(0.009m, result.VolumenTotalM3);
        Assert.Equal(tarifa.CuadroId, result.CuadroId);
        Assert.Equal(tarifa.Version, result.Version);
        Assert.Equal(100, result.Desglose.Total);
        Assert.Equal(2, bultos.Length);
        Assert.Single(tarifa.Lineas);
    }

    [Fact]
    public void SeleccionaExclusivamenteZonaYModalidadSolicitadas()
    {
        var tarifa = Tarifario(Linea(Guid.NewGuid(), precio: 999), Linea(precio: 100),
            Linea(modalidad: ModalidadServicio.Urgente, precio: 200));
        var bultos = new[] { new BultoAlta("A", 1, 10, 10, 10) };
        Assert.Equal(100, new CalculadorTarifa().Calcular(tarifa, Zona, ModalidadServicio.Estandar, bultos).Desglose.Total);
        Assert.Equal(200, new CalculadorTarifa().Calcular(tarifa, Zona, ModalidadServicio.Urgente, bultos).Desglose.Total);
        Assert.Throws<InvalidOperationException>(() => new CalculadorTarifa().Calcular(tarifa,
            Guid.NewGuid(), ModalidadServicio.Estandar, bultos));
    }

    [Fact]
    public void NoEligeTarifaPorBultoCuandoElTotalExcedeLosLimites()
    {
        var tarifa = Tarifario(Linea());
        Assert.Throws<InvalidOperationException>(() => new CalculadorTarifa().Calcular(tarifa, Zona,
            ModalidadServicio.Estandar, [new("A", 6, 10, 10, 10), new("B", 6, 10, 10, 10)]));
        Assert.Throws<InvalidOperationException>(() => new CalculadorTarifa().Calcular(tarifa, Zona,
            ModalidadServicio.Estandar, [new("A", 1, 100, 100, 60), new("B", 1, 100, 100, 60)]));
    }

    [Fact]
    public void AplicaLosAjustesDelDtoExistenteSinModificarlo()
    {
        var linea = Linea() with { Ajustes = [new(TipoAjuste.Recargo, 10), new(TipoAjuste.Bonificacion, 5)] };
        var result = new CalculadorTarifa().Calcular(Tarifario(linea), Zona, ModalidadServicio.Estandar,
            [new("A", 1, 10, 10, 10)]);
        Assert.Equal(new DesgloseTarifa(100, 10, 5, 105), result.Desglose);
        Assert.Equal(100, linea.Precio);
        Assert.Equal(2, linea.Ajustes.Count);
    }

    [Fact]
    public void RechazaEnvioVacioCodigosRepetidosYDimensionesInvalidas()
    {
        var calculator = new CalculadorTarifa();
        var tarifa = Tarifario(Linea());
        Assert.Throws<ArgumentException>(() => calculator.Calcular(tarifa, Zona, ModalidadServicio.Estandar, []));
        Assert.Throws<ArgumentException>(() => calculator.Calcular(tarifa, Zona, ModalidadServicio.Estandar,
            [new("A", 1, 10, 10, 10), new("a", 1, 10, 10, 10)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calcular(tarifa, Zona, ModalidadServicio.Estandar,
            [new("A", 1, 0, 10, 10)]));
    }
}
