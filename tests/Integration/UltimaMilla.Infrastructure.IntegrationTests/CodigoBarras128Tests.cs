using System.Text.RegularExpressions;
using UltimaMilla.Web.Compartido;

namespace UltimaMilla.Infrastructure.IntegrationTests;

// Code 128 de las etiquetas (lo escanea la PWA). Sin base de datos: vive acá porque este proyecto referencia la Web.
public sealed class CodigoBarras128Tests
{
    [Fact]
    public void LaTablaEsLaDelEstandar()
    {
        Assert.Equal(107, CodigoBarras128.Patrones.Count);
        // Cada símbolo: 3 barras y 3 espacios que suman 11 módulos (parada: 4 barras, 3 espacios, 13 módulos).
        Assert.All(CodigoBarras128.Patrones.Take(106), p => Assert.Equal((6, 11), (p.Length, p.Sum(c => c - '0'))));
        Assert.Equal((7, 13), (CodigoBarras128.Patrones[106].Length, CodigoBarras128.Patrones[106].Sum(c => c - '0')));
        // Sin repetidos: un error al copiar la tabla haría dos símbolos iguales o rompería la suma.
        Assert.Equal(107, CodigoBarras128.Patrones.Distinct().Count());
        // Barras de cada símbolo suman un número par de módulos (paridad del estándar).
        Assert.All(CodigoBarras128.Patrones.Take(106), p => Assert.Equal(0, (p[0] - '0' + p[2] - '0' + p[4] - '0') % 2));
    }

    [Fact]
    public void ElDigitoDeControlEsElDelEstandar()
    {
        // Valor = ASCII - 32; control = (104 + Σ valor·posición) mod 103. "PJJ123C": 879 mod 103 = 55.
        var simbolos = CodigoBarras128.Simbolos("PJJ123C")
            ?? throw new InvalidOperationException("El código ASCII de prueba debe producir símbolos.");
        Assert.Equal([104, 48, 42, 42, 17, 18, 19, 35, 55, 106], simbolos);
        var b001 = CodigoBarras128.Simbolos("B001")!;
        Assert.Equal((104, 106), (b001[0], b001[^1]));
        Assert.Equal((104 + 34 * 1 + 16 * 2 + 16 * 3 + 17 * 4) % 103, b001[^2]);
    }

    [Fact]
    public void ElSvgTieneLasBarrasDeCadaSimboloYNoIncluyeElTexto()
    {
        var svg = CodigoBarras128.Svg("B001<script>")!;
        Assert.StartsWith("<svg ", svg);
        Assert.DoesNotContain("script", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("B001", svg);
        // 3 barras por símbolo (inicio + 12 caracteres + control) y 4 de la parada, más el fondo.
        Assert.Equal(3 * 14 + 4 + 1, Regex.Matches(svg, "<rect ").Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ñandú")]
    [InlineData("con\nsalto")]
    public void FueraDelJuegoBNoSeGeneraYLaEtiquetaQuedaConElTexto(string texto) =>
        Assert.Null(CodigoBarras128.Svg(texto));
}
