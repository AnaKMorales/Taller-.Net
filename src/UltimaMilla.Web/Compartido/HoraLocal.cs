using UltimaMilla.Web.Pages.Backoffice;

namespace UltimaMilla.Web.Compartido;

// Fechas en hora de Uruguay, la misma que usan los cortes de período de reportes, cuenta corriente y liquidaciones.
// (Textos.Fecha usa la hora local del servidor; acá se muestra la del corte para que fecha y período coincidan.)
public static class HoraLocal
{
    public static string Fecha(DateTime? utc) =>
        utc is { } f ? (f + ReportesModel.HoraUruguay).ToString("dd/MM/yyyy HH:mm") : "—";

    public static DateOnly Hoy(TimeProvider reloj) =>
        DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime + ReportesModel.HoraUruguay);
}
