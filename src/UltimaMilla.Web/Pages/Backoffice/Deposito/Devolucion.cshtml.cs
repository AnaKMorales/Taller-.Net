using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Deposito;

namespace UltimaMilla.Web.Pages.Backoffice.Deposito;

[Authorize(Roles = "Administrador,Despachador,OperarioDeposito")]
public sealed class DevolucionModel(DevolucionDepositoHandler gestion) : OperacionDepositoModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public string? Codigo { get; set; }
    public EstadoDevolucionEnvioDto? Envio { get; private set; }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await CargarAsync(ct); }
        catch (Exception e) when (e is AislamientoOperadorException or KeyNotFoundException) { return NotFound(); }
        return Page();
    }
    public Task<IActionResult> OnPostIniciarAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        await gestion.IniciarAsync(Id, new(Version), ct);
        return "Devolución iniciada.";
    }, ct);
    public Task<IActionResult> OnPostEscanearAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var resultado = await gestion.EscanearAsync(Id, new(Codigo ?? "", Version), ct);
        return resultado.Repetido ? "El bulto ya estaba recibido." : "Bulto recibido en devolución.";
    }, ct);
    public Task<IActionResult> OnPostCompletarAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        await gestion.CompletarAsync(Id, new(Version), ct);
        return "Devolución completada.";
    }, ct);
    protected override async Task CargarAsync(CancellationToken ct) => Envio = await gestion.ConsultarAsync(Id, ct);
}
