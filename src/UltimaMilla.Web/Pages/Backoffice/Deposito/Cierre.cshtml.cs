using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Deposito;

namespace UltimaMilla.Web.Pages.Backoffice.Deposito;

[Authorize(Roles = "Administrador,Despachador,OperarioDeposito")]
public sealed class CierreModel(DevolucionDepositoHandler devoluciones, RendirRutaHandler rendiciones) : PageModel
{
    public IReadOnlyList<DevolucionAbiertaDto> Devoluciones { get; private set; } = [];
    public IReadOnlyList<RutaPorRendirDto> Rutas { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken ct)
    {
        Devoluciones = await devoluciones.ListarAbiertasAsync(ct);
        Rutas = await rendiciones.ListarAsync(ct);
    }
}
