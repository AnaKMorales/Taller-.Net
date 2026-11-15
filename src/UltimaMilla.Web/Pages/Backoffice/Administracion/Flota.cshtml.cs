using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Web.Compartido;
using UltimaMilla.Web.Pages.Backoffice.Deposito;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice.Administracion;

// Repartidores y vehículos del operador.
[Authorize(Policy = Politicas.Administracion)]
public sealed class FlotaModel(IAdministracionFlota flota) : OperacionDepositoModel
{
    public IReadOnlyList<RepartidorAdministracionDto> Repartidores { get; private set; } = [];
    public IReadOnlyList<VehiculoAdministracionDto> Vehiculos { get; private set; } = [];

    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string? Contrasena { get; set; }
    [BindProperty] public string? Matricula { get; set; }
    [BindProperty] public string? Peso { get; set; }
    [BindProperty] public string? Volumen { get; set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public Task<IActionResult> OnPostRepartidorAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var r = await flota.CrearRepartidorAsync(new CrearRepartidor(Email ?? "", Contrasena ?? ""), ct);
        return $"Repartidor {r.Email} creado. Ya puede ingresar a la app.";
    }, ct);

    public Task<IActionResult> OnPostVehiculoAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var v = await flota.CrearVehiculoAsync(new CrearVehiculo(Matricula ?? "",
            Numeros.Leer(Peso, "capacidad de peso"), Numeros.Leer(Volumen, "capacidad de volumen")), ct);
        return $"Vehículo {v.Matricula} creado.";
    }, ct);

    protected override async Task CargarAsync(CancellationToken ct)
    {
        Repartidores = await flota.RepartidoresAsync(ct);
        Vehiculos = await flota.VehiculosAsync(ct);
    }
}
