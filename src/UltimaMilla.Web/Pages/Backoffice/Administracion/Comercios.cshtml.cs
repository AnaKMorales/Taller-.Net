using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Web.Pages.Backoffice.Deposito;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice.Administracion;

// Comercios con cuenta en el operador.
[Authorize(Policy = Politicas.Administracion)]
public sealed class ComerciosModel(IAdministracionComercios comercios) : OperacionDepositoModel
{
    public IReadOnlyList<CuentaAdministracionDto> Cuentas { get; private set; } = [];

    [BindProperty] public string? RazonSocial { get; set; }
    [BindProperty] public string? Telefono { get; set; }
    [BindProperty] public string? Email { get; set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public Task<IActionResult> OnPostAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var c = await comercios.CrearAsync(new CrearComercio(RazonSocial ?? "", Telefono ?? "", Email ?? ""), ct);
        return $"Comercio {c.RazonSocial} creado. Agregale un usuario para que entre al portal.";
    }, ct);

    protected override async Task CargarAsync(CancellationToken ct) => Cuentas = await comercios.CuentasAsync(ct);
}
