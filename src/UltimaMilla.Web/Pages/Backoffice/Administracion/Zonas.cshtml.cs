using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Web.Pages.Backoffice.Deposito;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice.Administracion;

// Zonas de cobertura y sus franjas horarias.
[Authorize(Policy = Politicas.Administracion)]
public sealed class ZonasModel(IAdministracionZonas zonas) : OperacionDepositoModel
{
    public IReadOnlyList<ZonaAdministracionDto> Zonas { get; private set; } = [];

    [BindProperty] public Guid ZonaId { get; set; }
    [BindProperty] public string? Nombre { get; set; }
    [BindProperty] public string? CodigoPostal { get; set; }
    [BindProperty] public string? Franja { get; set; }
    [BindProperty] public TimeOnly? Desde { get; set; }
    [BindProperty] public TimeOnly? Hasta { get; set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public Task<IActionResult> OnPostZonaAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var z = await zonas.CrearZonaAsync(new CrearZonaCobertura(Nombre ?? "", CodigoPostal ?? "", [FranjaPedida()]), ct);
        return $"Zona {z.Nombre} creada.";
    }, ct);

    public Task<IActionResult> OnPostFranjaAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var f = await zonas.CrearFranjaAsync(ZonaId, FranjaPedida(), ct);
        return $"Franja {f.Nombre} agregada.";
    }, ct);

    // Cada zona necesita al menos una franja para ofrecer fechas de entrega
    private CrearFranjaHoraria FranjaPedida() =>
        Desde is { } desde && Hasta is { } hasta
            ? new CrearFranjaHoraria(Franja ?? "", desde, hasta)
            : throw new ArgumentException("Indicá el horario de la franja.");

    protected override async Task CargarAsync(CancellationToken ct) => Zonas = await zonas.ZonasAsync(ct);
}
