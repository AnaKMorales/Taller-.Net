using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Usuarios;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice;

// Detalle de un envío con el estado de cada bulto (requisito 31) y la gestión de bultos pendientes de una
// entrega parcial (requisitos 33 y 34). Las acciones usan IDespachoPendientes, que aplica las reglas del
// dominio, el aislamiento y el control de versiones; esta página solo arma la solicitud y muestra el resultado.
[Authorize(Policy = Politicas.Backoffice)]
public sealed class EnvioModel(IDetalleEnvio detalles, IDespachoPendientes despacho) : PageModel
{
    public const string RutaNueva = "nueva";

    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }

    public DetalleEnvioDto? Envio { get; private set; }
    public PlanificacionPendientesDto? Planificacion { get; private set; }
    public string? Error { get; private set; }
    [TempData] public string? Exito { get; set; }

    // Formulario de nueva visita. La ruta elegida viaja como "id|versión|próximo orden" para que la asignación
    // use la versión que vio el despachador: si otro la cambió, el núcleo rechaza con conflicto.
    [BindProperty] public Guid VersionEnvio { get; set; }
    [BindProperty] public string? Ruta { get; set; }
    [BindProperty] public Guid? RepartidorId { get; set; }
    [BindProperty] public Guid? VehiculoId { get; set; }
    [BindProperty] public int MaxParadas { get; set; } = 20;
    [BindProperty] public DateOnly? Fecha { get; set; }
    [BindProperty] public Guid? FranjaId { get; set; }

    // Las acciones de despacho son del administrador y el despachador; el operario de depósito solo consulta.
    public bool PuedeDespachar =>
        User.IsInRole(nameof(RolUsuario.Administrador)) || User.IsInRole(nameof(RolUsuario.Despachador));

    public bool OfreceNuevaVisita => Planificacion?.Accion is
        AccionBultosPendientes.NuevaVisitaSinRecargo or AccionBultosPendientes.NuevaVisitaConRecargo;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        if (!await CargarAsync(ct)) return NotFound();
        Fecha ??= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        FranjaId ??= Planificacion?.Franjas.FirstOrDefault()?.Id;
        Ruta ??= Planificacion?.Rutas.Count > 0 ? ValorRuta(Planificacion.Rutas[0]) : RutaNueva;
        return Page();
    }

    public async Task<IActionResult> OnPostAsignarAsync(CancellationToken ct)
    {
        if (!PuedeDespachar) return Forbid();
        return await EjecutarAsync(async () =>
        {
            var planificacion = await despacho.ConsultarAsync(Id, ct);
            var envio = await detalles.ObtenerAsync(Id, ct) ?? throw new AislamientoOperadorException();
            // Antes de crear una ruta nueva: si la nueva visita no corresponde, la ruta quedaría vacía.
            if (!envio.NuevaVisitaPosible)
                throw new InvalidOperationException($"No corresponde una nueva visita: {envio.SinNuevaVisita}.");
            if (Fecha is not { } fecha) throw new ArgumentException("Elegí la fecha de la nueva visita.");
            var franja = planificacion.Franjas.SingleOrDefault(f => f.Id == FranjaId)
                ?? throw new ArgumentException("Elegí una franja de la zona del envío.");

            RutaPlanificableDto ruta;
            if (Ruta == RutaNueva)
            {
                if (RepartidorId is not { } repartidor || VehiculoId is not { } vehiculo)
                    throw new ArgumentException("Para una ruta nueva elegí repartidor y vehículo.");
                ruta = await despacho.CrearRutaAsync(new CrearRutaPendientes(repartidor, vehiculo, MaxParadas), ct);
            }
            else
            {
                var elegida = LeerRuta(Ruta);
                ruta = elegida with { Repartidor = planificacion.Rutas.FirstOrDefault(r => r.Id == elegida.Id)?.Repartidor ?? "" };
            }

            // La ventana se arma en la hora local del operador: la misma que la programación actual del envío.
            var desfase = envio.VentanaDesde.Offset;
            var inicio = new DateTimeOffset(fecha.ToDateTime(franja.Desde), desfase);
            var fin = new DateTimeOffset(fecha.ToDateTime(franja.Hasta), desfase);
            var visita = await despacho.AsignarAsync(ruta.Id, new AsignarPendientes(Id, VersionEnvio, ruta.Version,
                ruta.ProximoOrden, inicio, new ProgramacionPendientes(franja.Id, fecha, inicio, fin)), ct);

            var recargo = visita.RecargoPrevisto > 0 ? $" Recargo previsto: {Compartido.Textos.Importe(visita.RecargoPrevisto)}." : "";
            return $"Se asignaron {visita.Bultos.Count} bultos pendientes a la ruta de {ruta.Repartidor} " +
                   $"para el {fecha:dd/MM}, entre {franja.Desde:HH\\:mm} y {franja.Hasta:HH\\:mm}.{recargo}";
        }, ct);
    }

    public async Task<IActionResult> OnPostDevolverAsync(CancellationToken ct)
    {
        if (!PuedeDespachar) return Forbid();
        return await EjecutarAsync(async () =>
        {
            var devolucion = await despacho.DevolverAsync(Id, VersionEnvio, ct);
            return $"Se inició la devolución al comercio de {devolucion.Bultos.Count} bultos pendientes.";
        }, ct);
    }

    private async Task<IActionResult> EjecutarAsync(Func<Task<string>> accion, CancellationToken ct)
    {
        try
        {
            Exito = await accion();
            return RedirectToPage(new { Id });
        }
        catch (ConflictoConcurrenciaException)
        {
            Error = "Otra persona modificó este envío o la ruta mientras lo mirabas. Revisá los datos actualizados y volvé a intentar.";
        }
        catch (AislamientoOperadorException)
        {
            Error = "El repartidor, el vehículo o la ruta elegidos no están disponibles para tu operador.";
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            // Los mensajes del núcleo ya explican el motivo (capacidad del vehículo, plazo entre intentos, etc.).
            Error = error.Message;
        }
        if (!await CargarAsync(ct)) return NotFound();
        return Page();
    }

    private async Task<bool> CargarAsync(CancellationToken ct)
    {
        Envio = await detalles.ObtenerAsync(Id, ct);
        if (Envio is null) return false;
        if (PuedeDespachar && Envio.AdmiteGestionDePendientes)
        {
            try
            {
                Planificacion = await despacho.ConsultarAsync(Id, ct);
                VersionEnvio = Planificacion.VersionEnvio;
            }
            catch (InvalidOperationException)
            {
                // El núcleo no lo considera gestionable (por ejemplo, cambió entre las dos lecturas).
                Planificacion = null;
            }
        }
        return true;
    }

    public static string ValorRuta(RutaPlanificableDto r) => $"{r.Id}|{r.Version}|{r.ProximoOrden}";

    private static RutaPlanificableDto LeerRuta(string? valor)
    {
        var partes = valor?.Split('|');
        if (partes is not { Length: 3 } || !Guid.TryParse(partes[0], out var id) || !Guid.TryParse(partes[1], out var version) ||
            !int.TryParse(partes[2], out var orden))
            throw new ArgumentException("Elegí una ruta.");
        return new RutaPlanificableDto(id, version, "", "", orden);
    }
}
