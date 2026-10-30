using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Portal;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Portal;

[Authorize(Policy = Politicas.Portal)]
public sealed class IndexModel(IConsultasPortal consultas) : PageModel
{
    public IReadOnlyList<EnvioPortalDto> Envios { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Envios = await consultas.EnviosAsync(cancellationToken);
}
