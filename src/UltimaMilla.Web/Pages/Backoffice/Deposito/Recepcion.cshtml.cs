using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Deposito;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice.Deposito;

// Recepción de un envío: escanear cada bulto, anotar discrepancias y confirmar el ingreso al depósito.
[Authorize(Policy = Politicas.Backoffice)]
public sealed class RecepcionModel(RecepcionDepositoHandler recepcion) : OperacionDepositoModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public string? Codigo { get; set; }
    [BindProperty] public TipoDiscrepancia Tipo { get; set; }
    [BindProperty] public Guid? BultoId { get; set; }
    [BindProperty] public string? Detalle { get; set; }
    [BindProperty] public string? Declarado { get; set; }
    [BindProperty] public string? Recibido { get; set; }

    public RecepcionDepositoDto? Envio { get; private set; }
    public bool Abierta => Envio?.Estado == EstadoEnvio.Admitido;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        try { await CargarAsync(ct); }
        catch (Exception e) when (e is KeyNotFoundException or Application.Abstractions.AislamientoOperadorException) { return NotFound(); }
        return Page();
    }

    public Task<IActionResult> OnPostEscanearAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var r = await recepcion.EscanearAsync(Id, new EscanearRecepcionCommand(Codigo ?? "", Version), ct);
        return r.Repetido
            ? $"El bulto {Codigo} ya estaba escaneado."
            : $"Bulto {Codigo} recibido ({r.Recepcion.Recibidos} de {r.Recepcion.Esperados}).";
    }, ct);

    public Task<IActionResult> OnPostDiscrepanciaAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        await recepcion.RegistrarDiscrepanciaAsync(Id, new RegistrarDiscrepanciaCommand(Version, Tipo,
            Detalle ?? "", Declarado ?? "", Recibido ?? "", BultoId), ct);
        return "Discrepancia registrada.";
    }, ct);

    public Task<IActionResult> OnPostConfirmarAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var r = await recepcion.ConfirmarAsync(Id, new ConfirmarRecepcionCommand(Version), ct);
        return $"Envío {r.CodigoSeguimiento} ingresado al depósito.";
    }, ct, "/backoffice/deposito");

    protected override async Task CargarAsync(CancellationToken ct) => Envio = await recepcion.ConsultarAsync(Id, ct);
}
