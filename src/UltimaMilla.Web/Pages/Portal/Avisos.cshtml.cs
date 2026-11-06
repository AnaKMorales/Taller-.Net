using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Portal;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Portal;

[Authorize(Policy = Politicas.Portal)]
public sealed class AvisosModel(IPanelAvisos panel) : PageModel
{
    public IReadOnlyList<SuscripcionDto> Suscripciones { get; private set; } = [];
    public IReadOnlyList<AvisoDto> Avisos { get; private set; } = [];
    public SuscripcionCreada? Creada { get; private set; }
    public string? Error { get; private set; }

    [BindProperty] public string? Destino { get; set; }
    [BindProperty] public List<TipoEvento> Eventos { get; set; } = [];

    public Task OnGetAsync(CancellationToken cancellationToken) => CargarAsync(cancellationToken);

    public async Task<IActionResult> OnPostCrearAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(Destino?.Trim(), UriKind.Absolute, out var destino))
            Error = "La dirección no es válida. Ejemplo: https://mitienda.com/avisos";
        else
        {
            try
            {
                Creada = await panel.CrearSuscripcionAsync(destino, Eventos, cancellationToken);
                Destino = null;
                Eventos = [];
            }
            catch (ArgumentException)
            {
                Error = "Revisá la dirección (http o https) y elegí al menos un evento.";
            }
        }
        // Sin redirección: el secreto se muestra en esta respuesta y no se guarda en ningún otro lado.
        await CargarAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostDesactivarAsync(Guid id, CancellationToken cancellationToken)
    {
        await panel.DesactivarSuscripcionAsync(id, cancellationToken);
        return RedirectToPage();
    }

    private async Task CargarAsync(CancellationToken cancellationToken)
    {
        Suscripciones = await panel.SuscripcionesAsync(cancellationToken);
        Avisos = await panel.UltimosAvisosAsync(50, cancellationToken);
    }
}
