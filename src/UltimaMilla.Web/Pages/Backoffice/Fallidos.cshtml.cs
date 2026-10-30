using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice;

[Authorize(Policy = Politicas.Backoffice)]
public sealed class FallidosModel(IConsultasBackoffice consultas) : PageModel
{
    public IReadOnlyList<AvisoFallidoDto> Avisos { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Avisos = await consultas.AvisosFallidosAsync(cancellationToken);
}
