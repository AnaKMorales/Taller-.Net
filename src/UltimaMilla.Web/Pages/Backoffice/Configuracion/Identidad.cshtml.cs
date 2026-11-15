using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;

namespace UltimaMilla.Web.Pages.Backoffice.Configuracion;

[Authorize(Roles = "Administrador")]
public sealed class IdentidadModel(IAdministracionIdentidadOperador gestion) : PageModel
{
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public string Marca { get; set; } = "";
    [BindProperty] public string Primario { get; set; } = "#1F3A5F";
    [BindProperty] public string Secundario { get; set; } = "#F5F7FA";
    [BindProperty] public string? Telefono { get; set; }
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string? Logo { get; set; }
    public string? Error { get; private set; }
    [TempData] public string? Exito { get; set; }
    public async Task OnGetAsync(CancellationToken ct)
    {
        var estado = await gestion.ConsultarAsync(ct);
        Version = estado.Version; Marca = estado.Datos.Marca;
        Primario = estado.Datos.ColorPrimario; Secundario = estado.Datos.ColorSecundario;
        Telefono = estado.Datos.TelefonoContacto; Email = estado.Datos.EmailContacto; Logo = estado.Datos.LogoUrl;
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) { Error = "Revisá los campos del formulario."; return Page(); }
        try
        {
            await gestion.ModificarAsync(new(new(Marca, Primario, Secundario, Telefono, Email, Logo), Version), ct);
            Exito = "Identidad visual guardada. Se aplica al portal y al seguimiento al recargar las páginas.";
            return RedirectToPage();
        }
        catch (ConflictoConcurrenciaException) { Error = "Otro administrador cambió la identidad. Recargá la página y volvé a aplicar tus cambios."; }
        catch (ArgumentException e) { Error = e.Message; }
        return Page();
    }
}
