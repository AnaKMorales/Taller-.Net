namespace UltimaMilla.Web.Compartido;

public static class TemaOperador
{
    // El dominio admite exclusivamente colores #RRGGBB. Elegir texto negro o blanco
    // mantiene legibles los colores claros y oscuros que configura el administrador.
    public static string Fondo(string color) => $"background-color:{color};color:{Texto(color)}";
    private static string Texto(string color)
    {
        static double Canal(string texto)
        {
            var valor = Convert.ToInt32(texto, 16) / 255.0;
            return valor <= .04045 ? valor / 12.92 : Math.Pow((valor + .055) / 1.055, 2.4);
        }
        var luminancia = .2126 * Canal(color.Substring(1, 2)) + .7152 * Canal(color.Substring(3, 2)) + .0722 * Canal(color.Substring(5, 2));
        return luminancia > .179 ? "#000000" : "#FFFFFF";
    }
}
