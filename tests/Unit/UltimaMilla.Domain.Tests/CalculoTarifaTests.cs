using System.Globalization;

namespace UltimaMilla.Domain.Tests;

public sealed class CalculoTarifaTests
{
    [Theory]
    [InlineData("0.5", "100")]
    [InlineData("9.999", "100")]
    [InlineData("10", "200")]
    [InlineData("19.999", "200")]
    public void PesoRespetaLimitesInferiorIncluidoYSuperiorExcluido(string peso, string precio)
    {
        var result = CalculadoraTarifa.Calcular(Numero(peso), 1,
            [new(0, 10, 1, 100, []), new(10, 20, 1, 200, [])]);
        Assert.Equal(Numero(precio), result.Total);
    }

    [Fact]
    public void PesoFueraDelUltimoTramoNoTieneTarifa() =>
        Assert.Throws<InvalidOperationException>(() => CalculadoraTarifa.Calcular(20, 1,
            [new(0, 10, 1, 100, []), new(10, 20, 1, 200, [])]));

    [Fact]
    public void VolumenMaximoSeIncluyeYExcederloRechazaElEnvio()
    {
        var tramo = new TramoCalculoTarifa(0, 10, 1, 100, []);
        Assert.Equal(100, CalculadoraTarifa.Calcular(1, 1, [tramo]).Total);
        Assert.Throws<InvalidOperationException>(() => CalculadoraTarifa.Calcular(1, 1.000001m, [tramo]));
    }

    [Fact]
    public void ConfiguracionAmbiguaSeRechazaEnVezDeElegirLaPrimera() =>
        Assert.Throws<InvalidOperationException>(() => CalculadoraTarifa.Calcular(5, 1,
            [new(0, 10, 1, 100, []), new(0, 10, 2, 200, [])]));

    [Fact]
    public void AjustesSeAplicanSobreLaBaseYNoDependenDelOrden()
    {
        AjusteCalculoTarifa[] ajustes = [new(TipoAjuste.Recargo, 10), new(TipoAjuste.Bonificacion, 5), new(TipoAjuste.Recargo, 20)];
        var result = CalculadoraTarifa.Calcular(1, 1, [new(0, 10, 1, 100, ajustes)]);
        Assert.Equal(new DesgloseTarifa(100, 30, 5, 125), result);
        Assert.Equal(result, CalculadoraTarifa.Calcular(1, 1, [new(0, 10, 1, 100, ajustes.Reverse().ToArray())]));
    }

    [Theory]
    [InlineData("1.005", "1.01")]
    [InlineData("1.004", "1.00")]
    public void RedondeaElTotalADosDecimalesAlejandoElPuntoMedioDeCero(string precio, string total) =>
        Assert.Equal(Numero(total), CalculadoraTarifa.Calcular(1, 1, [new(0, 10, 1, Numero(precio), [])]).Total);

    [Fact]
    public void NoRedondeaCadaAjusteAntesDeCalcularElTotal()
    {
        var result = CalculadoraTarifa.Calcular(1, 1,
            [new(0, 10, 1, 0.05m, [new(TipoAjuste.Recargo, 10), new(TipoAjuste.Recargo, 10)])]);
        Assert.Equal(0.01m, result.Recargos);
        Assert.Equal(0.06m, result.Total);
    }

    [Fact]
    public void BonificacionTotalPermiteTarifaCero() =>
        Assert.Equal(0, CalculadoraTarifa.Calcular(1, 1,
            [new(0, 10, 1, 100, [new(TipoAjuste.Bonificacion, 100)])]).Total);

    [Fact]
    public void CombinacionQueProducePrecioNegativoSeRechaza() =>
        Assert.Throws<InvalidOperationException>(() => CalculadoraTarifa.Calcular(1, 1,
            [new(0, 10, 1, 100, [new(TipoAjuste.Bonificacion, 60), new(TipoAjuste.Bonificacion, 50)])]));

    [Fact]
    public void BonificacionIndividualMayorACienSeRechaza() =>
        Assert.Throws<ArgumentException>(() => CalculadoraTarifa.Calcular(1, 1,
            [new(0, 10, 1, 100, [new(TipoAjuste.Bonificacion, 101)])]));

    private static decimal Numero(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
