using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Deposito;

namespace UltimaMilla.Web.Pages.Backoffice.Deposito;

[Authorize(Roles = "Administrador,Despachador,OperarioDeposito")]
public sealed class RendicionModel(RendirRutaHandler gestion) : OperacionDepositoModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public Guid Version { get; set; }
    public EstadoRendicionRutaDto? Ruta { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await CargarAsync(ct); }
        catch (Exception e) when (e is AislamientoOperadorException or KeyNotFoundException) { return NotFound(); }
        return Page();
    }
    public Task<IActionResult> OnPostRendirAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        await gestion.Handle(Id, new(Version), ct);
        return "Ruta rendida correctamente.";
    }, ct);
    protected override async Task CargarAsync(CancellationToken ct) => Ruta = await gestion.ConsultarAsync(Id, ct);
}
