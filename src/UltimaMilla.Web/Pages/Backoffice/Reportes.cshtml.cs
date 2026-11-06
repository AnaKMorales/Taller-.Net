using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice;

[Authorize(Policy = Politicas.Administracion)]
public sealed class ReportesModel(IReportesGestion reportes, TimeProvider reloj) : PageModel
{
    // Uruguay no tiene horario de verano: el desfase es fijo.
    public static readonly TimeSpan HoraUruguay = TimeSpan.FromHours(-3);
    public const int MaximoDias = 366;

    [BindProperty(SupportsGet = true)] public DateOnly? Desde { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? Hasta { get; set; }

    public ReporteGestion? Reporte { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime + HoraUruguay);
        Hasta ??= hoy;
        Desde ??= Hasta.Value.AddDays(-29);

        if (Hasta < Desde) Error = "La fecha \"hasta\" no puede ser anterior a \"desde\".";
        else if (Hasta.Value.DayNumber - Desde.Value.DayNumber >= MaximoDias) Error = "El período no puede superar un año.";
        else Reporte = await reportes.GenerarAsync(Desde.Value, Hasta.Value, HoraUruguay, cancellationToken);
    }
}
