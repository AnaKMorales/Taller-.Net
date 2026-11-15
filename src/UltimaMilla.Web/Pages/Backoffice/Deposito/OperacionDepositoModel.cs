using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Web.Pages.Backoffice.Deposito;

// Base de las pantallas de depósito: ejecuta la acción, redirige (PRG) y traduce los errores del núcleo.
public abstract class OperacionDepositoModel : PageModel
{
    public string? Error { get; protected set; }
    [TempData] public string? Exito { get; set; }

    // Asignar, crear rutas y despachar: administrador y despachador. El operario recibe y carga.
    public bool PuedeDespachar =>
        User.IsInRole(nameof(RolUsuario.Administrador)) || User.IsInRole(nameof(RolUsuario.Despachador));

    protected abstract Task CargarAsync(CancellationToken ct);

    protected async Task<IActionResult> EjecutarAsync(Func<Task<string>> accion, CancellationToken ct, string? volverA = null)
    {
        try
        {
            Exito = await accion();
            return volverA is null ? RedirectToPage() : LocalRedirect(volverA);
        }
        catch (ConflictoConcurrenciaException)
        {
            Error = "Otra persona lo modificó mientras lo mirabas. Revisá los datos actualizados y volvé a intentar.";
        }
        catch (AislamientoOperadorException)
        {
            Error = "No está disponible para tu operador.";
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            Error = e.Message;
        }
        await CargarAsync(ct);
        return Page();
    }
}
