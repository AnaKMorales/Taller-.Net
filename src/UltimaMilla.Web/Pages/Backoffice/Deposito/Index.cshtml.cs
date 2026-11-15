using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice.Deposito;

// Circuito del depósito: recibir, asignar a una ruta y preparar rutas para despachar.
[Authorize(Policy = Politicas.Backoffice)]
public sealed class IndexModel(IConsultasBackoffice consultas, AsignarEnvioRutaHandler asignacion,
    IDespachoPendientes despacho) : OperacionDepositoModel
{
    public IReadOnlyList<EnvioPorRecibirDto> PorRecibir { get; private set; } = [];
    public IReadOnlyList<EnvioAsignableDto> Disponibles { get; private set; } = [];
    public IReadOnlyList<RutaAsignacionDto> Rutas { get; private set; } = [];
    public RecursosAsignacionDto? Recursos { get; private set; }

    [BindProperty] public Guid EnvioId { get; set; }
    [BindProperty] public Guid VersionEnvio { get; set; }
    [BindProperty] public string? Ruta { get; set; }
    [BindProperty] public Guid? RepartidorId { get; set; }
    [BindProperty] public Guid? VehiculoId { get; set; }
    [BindProperty] public int MaxParadas { get; set; } = 20;

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostAsignarAsync(CancellationToken ct)
    {
        if (!PuedeDespachar) return Forbid();
        return await EjecutarAsync(async () =>
        {
            // La ruta viaja como "id|versión" para detectar si alguien la cambió
            var partes = (Ruta ?? "").Split('|');
            if (partes.Length != 2 || !Guid.TryParse(partes[0], out var rutaId) || !Guid.TryParse(partes[1], out var versionRuta))
                throw new ArgumentException("Elegí una ruta.");
            var ruta = (await asignacion.ListarRutasAsync(ct)).SingleOrDefault(r => r.Id == rutaId)
                ?? throw new InvalidOperationException("La ruta ya no está en preparación.");
            var envio = (await asignacion.ListarEnviosAsync(ct)).SingleOrDefault(e => e.Id == EnvioId)
                ?? throw new InvalidOperationException("El envío ya no está disponible para asignar.");
            var llegada = new DateTimeOffset(DateTime.SpecifyKind(envio.VentanaDesde, DateTimeKind.Utc));
            await asignacion.Handle(rutaId, new AsignarEnvioRutaCommand(EnvioId, VersionEnvio, versionRuta,
                ruta.ProximoOrden, llegada), ct);
            return $"Envío {envio.CodigoSeguimiento} asignado a la ruta de {ruta.Repartidor} (parada {ruta.ProximoOrden}).";
        }, ct);
    }

    public async Task<IActionResult> OnPostCrearRutaAsync(CancellationToken ct)
    {
        if (!PuedeDespachar) return Forbid();
        return await EjecutarAsync(async () =>
        {
            if (RepartidorId is not { } repartidor || VehiculoId is not { } vehiculo)
                throw new ArgumentException("Elegí repartidor y vehículo.");
            var ruta = await despacho.CrearRutaAsync(new CrearRutaPendientes(repartidor, vehiculo, MaxParadas), ct);
            return $"Ruta creada para {ruta.Repartidor} con el vehículo {ruta.Matricula}.";
        }, ct);
    }

    protected override async Task CargarAsync(CancellationToken ct)
    {
        PorRecibir = await consultas.PorRecibirAsync(ct);
        Disponibles = await asignacion.ListarEnviosAsync(ct);
        Rutas = await asignacion.ListarRutasAsync(ct);
        if (PuedeDespachar) Recursos = await asignacion.RecursosAsync(ct);
    }
}
