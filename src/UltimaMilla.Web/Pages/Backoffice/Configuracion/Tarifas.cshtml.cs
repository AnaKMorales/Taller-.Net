using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Configuracion;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice.Configuracion;

// Cuadro tarifario editable desde la interfaz. Nunca se modifica una versión existente: "Publicar" crea una versión nueva. Los envíos ya admitidos conservan la suya.
[Authorize(Policy = Politicas.Administracion)]
public sealed class TarifasModel(IConfiguracionVersionada configuracion) : PageModel
{
    public IReadOnlyList<VersionConfiguracionDto> Versiones { get; private set; } = [];
    public IReadOnlyList<ZonaConfiguracionDto> Zonas { get; private set; } = [];
    public DetalleTarifarioDto? Vigente { get; private set; }
    public DetalleTarifarioDto? Base { get; private set; }
    public string? Error { get; private set; }
    [TempData] public string? Exito { get; set; }

    // Versión desde la que se arma el formulario; por defecto, la vigente.
    [BindProperty(SupportsGet = true)] public Guid? BaseId { get; set; }
    [BindProperty] public int UltimaVersionEsperada { get; set; }
    [BindProperty] public List<LineaFormulario> Lineas { get; set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CargarAsync(ct);
        Lineas = Base?.Lineas.Select(LineaFormulario.Desde).ToList() ?? [];
        if (Lineas.Count == 0) Lineas.Add(new LineaFormulario { ZonaId = Zonas.FirstOrDefault()?.Id ?? Guid.Empty });
    }

    public async Task<IActionResult> OnPostAgregarLineaAsync(CancellationToken ct)
    {
        await CargarAsync(ct, conservarEsperada: true);
        Lineas.Add(new LineaFormulario { ZonaId = Zonas.FirstOrDefault()?.Id ?? Guid.Empty });
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostPublicarAsync(CancellationToken ct)
    {
        try
        {
            var lineas = Lineas.Where(l => !l.Quitar).Select((l, i) => l.ADto(i + 1)).ToList();
            var creada = await configuracion.CrearTarifarioAsync(new NuevaVersionTarifario(UltimaVersionEsperada, null, lineas), ct);
            Exito = $"Se publicó la versión {creada.Version.Version} del cuadro tarifario. Los envíos nuevos ya la usan; " +
                "los envíos anteriores conservan la tarifa con la que se admitieron.";
            if (creada.RevisarInvalidacionCache)
                Exito += " No se pudo limpiar la caché: el cambio puede tardar hasta 30 minutos en verse.";
            return RedirectToPage();
        }
        catch (ConflictoConcurrenciaException)
        {
            Error = "Otro administrador publicó una versión mientras editabas. Recargá la página y volvé a aplicar tus cambios.";
        }
        catch (AislamientoOperadorException)
        {
            Error = "Una de las zonas elegidas no pertenece a tu operador.";
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            // Los mensajes del dominio ya están pensados para el usuario (rangos superpuestos, precio negativo, etc.).
            Error = error.Message;
        }
        await CargarAsync(ct, conservarEsperada: true);
        return Page();
    }

    private async Task CargarAsync(CancellationToken ct, bool conservarEsperada = false)
    {
        Versiones = (await configuracion.ListarTarifariosAsync(ct)).OrderByDescending(v => v.Version).ToList();
        Zonas = await configuracion.ListarZonasAsync(ct);
        Vigente = await configuracion.TarifarioVigenteAsync(ct);
        Base = BaseId is { } id ? await configuracion.ObtenerTarifarioAsync(id, ct) : Vigente;
        // La versión esperada es la última creada (incluidas las programadas). Si al publicar ya hay otra,
        // el servicio rechaza con conflicto en lugar de pisar el cambio ajeno.
        if (!conservarEsperada) UltimaVersionEsperada = Versiones.Count == 0 ? 0 : Versiones.Max(v => v.Version);
    }

    public string Zona(Guid id) => Zonas.FirstOrDefault(z => z.Id == id)?.Nombre ?? "—";

    public string EstadoVersion(VersionConfiguracionDto v) =>
        v.Id == Vigente?.Version.Id ? "Vigente" : v.VigenteDesde > DateTime.UtcNow ? "Programada" : "Anterior";
}

// Una línea del cuadro. Los ajustes del dominio son porcentajes que se suman sobre el precio base,
// así que alcanza con un total de recargo y uno de bonificación por línea.
public sealed class LineaFormulario
{
    public Guid ZonaId { get; set; }
    public ModalidadServicio Modalidad { get; set; }
    public string? PesoDesde { get; set; }
    public string? PesoHasta { get; set; }
    public string? VolumenHasta { get; set; }
    public string? Precio { get; set; }
    public string? Recargo { get; set; }
    public string? Bonificacion { get; set; }
    public bool Quitar { get; set; }

    public static LineaFormulario Desde(LineaTarifaDto l) => new()
    {
        ZonaId = l.ZonaId,
        Modalidad = l.Modalidad,
        PesoDesde = Numeros.Texto(l.PesoDesde),
        PesoHasta = Numeros.Texto(l.PesoHasta),
        VolumenHasta = Numeros.Texto(l.VolumenHasta),
        Precio = Numeros.Texto(l.Precio),
        Recargo = Total(l, TipoAjuste.Recargo),
        Bonificacion = Total(l, TipoAjuste.Bonificacion)
    };

    public LineaTarifaDto ADto(int numero)
    {
        var ajustes = new List<AjusteTarifaDto>();
        if (Numeros.LeerOpcional(Recargo, $"recargo de la línea {numero}") is > 0 and var recargo)
            ajustes.Add(new AjusteTarifaDto(TipoAjuste.Recargo, recargo));
        if (Numeros.LeerOpcional(Bonificacion, $"bonificación de la línea {numero}") is > 0 and var bonificacion)
            ajustes.Add(new AjusteTarifaDto(TipoAjuste.Bonificacion, bonificacion));
        return new LineaTarifaDto(ZonaId, Modalidad,
            Numeros.Leer(PesoDesde, $"peso desde de la línea {numero}"),
            Numeros.Leer(PesoHasta, $"peso hasta de la línea {numero}"),
            Numeros.Leer(VolumenHasta, $"volumen hasta de la línea {numero}"),
            Numeros.Leer(Precio, $"precio de la línea {numero}"), ajustes);
    }

    private static string Total(LineaTarifaDto l, TipoAjuste tipo)
    {
        var total = l.Ajustes.Where(a => a.Tipo == tipo).Sum(a => a.ValorPorcentaje);
        return total == 0 ? "" : Numeros.Texto(total);
    }
}
