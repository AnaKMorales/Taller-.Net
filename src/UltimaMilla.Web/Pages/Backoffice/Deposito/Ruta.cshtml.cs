using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice.Deposito;

// Carga de la ruta (escanear cada bulto al subirlo al vehículo) y despacho.
[Authorize(Policy = Politicas.Backoffice)]
public sealed class RutaModel(RegistrarCargaRutaHandler carga, DespacharRutaHandler despacho,
    AsignarEnvioRutaHandler asignacion) : OperacionDepositoModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public string? Codigo { get; set; }

    public EstadoCargaRutaDto? Ruta { get; private set; }
    public RutaAsignacionDto? Datos { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await CargarAsync(ct); }
        catch (Exception e) when (e is KeyNotFoundException or AislamientoOperadorException) { return NotFound(); }
        return Page();
    }

    public Task<IActionResult> OnPostCargarAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var r = await carga.Handle(Id, new RegistrarCargaRutaCommand(Codigo ?? "", Version), ct);
        return r.Repetido
            ? $"El bulto {Codigo} ya estaba cargado."
            : $"Bulto {Codigo} cargado ({r.Cargados} de {r.Esperados}).";
    }, ct);

    public async Task<IActionResult> OnPostDespacharAsync(CancellationToken ct)
    {
        if (!PuedeDespachar) return Forbid();
        return await EjecutarAsync(async () =>
        {
            var r = await despacho.Handle(Id, new DespacharRutaCommand(Version), ct);
            return $"Ruta despachada a las {r.DespachadaEn.ToLocalTime():HH:mm}. El repartidor ya la ve en su app.";
        }, ct, "/backoffice/deposito");
    }

    protected override async Task CargarAsync(CancellationToken ct)
    {
        Ruta = await carga.ConsultarAsync(Id, ct);
        Datos = (await asignacion.ListarRutasAsync(ct)).SingleOrDefault(r => r.Id == Id);
    }
}
