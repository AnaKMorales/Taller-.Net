using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice;

[Authorize(Policy = Politicas.Backoffice)]
public sealed class IncidenciasModel(IConsultasBackoffice consultas) : PageModel
{
    public IReadOnlyList<IncidenciaDto> Incidencias { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Incidencias = await consultas.IncidenciasAsync(cancellationToken);
}
