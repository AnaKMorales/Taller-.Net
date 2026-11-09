using System.Globalization;

namespace UltimaMilla.Web.Pages.Backoffice.Configuracion;

// Los números de los formularios se leen y escriben siempre en formato invariante ("1.5").
// Si se usara el binding decimal de MVC, dependería de la cultura del servidor: en es-UY "1.5"
// se interpretaría como 15. Se acepta también la coma porque es lo que escribe un usuario local.
internal static class Numeros
{
    public static string Texto(decimal valor) => valor.ToString("0.####", CultureInfo.InvariantCulture);

    public static string Texto(decimal? valor) => valor is { } v ? Texto(v) : "";

    public static decimal Leer(string? texto, string campo)
    {
        if (decimal.TryParse(texto?.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var valor))
            return valor;
        throw new ArgumentException($"El valor de «{campo}» no es un número válido.");
    }

    public static decimal? LeerOpcional(string? texto, string campo) =>
        string.IsNullOrWhiteSpace(texto) ? null : Leer(texto, campo);
}
