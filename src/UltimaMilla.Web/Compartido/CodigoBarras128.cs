using System.Globalization;
using System.Text;

namespace UltimaMilla.Web.Compartido;

// Código de barras Code 128 (juego B: ASCII 32 a 126) como SVG, para las etiquetas de bulto. Lo lee la cámara de la
// PWA del repartidor (BarcodeDetector) o cualquier lector. Sin librerías ni recursos externos: se imprime sin internet.
// El SVG solo contiene rectángulos calculados: el texto del código nunca se inserta en el marcado.
public static class CodigoBarras128
{
    // Anchos de barra/espacio de cada símbolo (0 a 106), según el estándar. Cada uno suma 11 módulos; el de parada, 13.
    public static readonly IReadOnlyList<string> Patrones =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
    ];

    private const int InicioB = 104, Parada = 106, ZonaSilencio = 10;

    // Valores de símbolo: inicio B, datos, dígito de control (módulo 103) y parada. null si hay caracteres fuera de B.
    public static int[]? Simbolos(string texto)
    {
        if (string.IsNullOrEmpty(texto) || texto.Any(c => c is < ' ' or > '~')) return null;
        var datos = texto.Select(c => c - 32).ToArray();
        var control = (InicioB + datos.Select((v, i) => v * (i + 1)).Sum()) % 103;
        return [InicioB, .. datos, control, Parada];
    }

    // null si el texto no se puede codificar (la etiqueta sigue mostrando el código en texto).
    public static string? Svg(string texto, int alto = 48, int modulo = 2)
    {
        if (Simbolos(texto) is not { } simbolos) return null;
        var barras = new StringBuilder();
        var x = ZonaSilencio;
        foreach (var simbolo in simbolos)
        {
            var patron = Patrones[simbolo];
            for (var i = 0; i < patron.Length; i++)
            {
                var ancho = patron[i] - '0';
                // Posiciones pares: barra; impares: espacio.
                if (i % 2 == 0) barras.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x * modulo}\" y=\"0\" width=\"{ancho * modulo}\" height=\"{alto}\"/>");
                x += ancho;
            }
        }
        var total = (x + ZonaSilencio) * modulo;
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" role=\"img\" aria-label=\"Código de barras del bulto\" " +
               $"width=\"{total}\" height=\"{alto}\" viewBox=\"0 0 {total} {alto}\" shape-rendering=\"crispEdges\">" +
               $"<rect width=\"{total}\" height=\"{alto}\" fill=\"#fff\"/><g fill=\"#000\">{barras}</g></svg>";
    }
}
