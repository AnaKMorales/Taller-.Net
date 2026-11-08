using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Configuracion;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice.Configuracion;

// Reglas operativas editables desde la interfaz (requisitos 4, 33 y sección 6.6): intentos, pruebas exigidas,
// motivos de no entrega, devolución, plazos y entrega parcial. Igual que el tarifario, se publican versiones nuevas.
[Authorize(Policy = Politicas.Administracion)]
public sealed class ReglasModel(IConfiguracionVersionada configuracion) : PageModel
{
    public IReadOnlyList<VersionConfiguracionDto> Versiones { get; private set; } = [];
    public DetalleReglasDto? Vigente { get; private set; }
    public DetalleReglasDto? Base { get; private set; }
    public string? Error { get; private set; }
    [TempData] public string? Exito { get; set; }

    [BindProperty(SupportsGet = true)] public Guid? BaseId { get; set; }
    [BindProperty] public int UltimaVersionEsperada { get; set; }
    [BindProperty] public ReglasFormulario Formulario { get; set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        await CargarAsync(ct);
        Formulario = Base is null ? ReglasFormulario.PorDefecto() : ReglasFormulario.Desde(Base.Datos);
    }

    public async Task<IActionResult> OnPostAgregarMotivoAsync(CancellationToken ct)
    {
        await CargarAsync(ct, conservarEsperada: true);
        Formulario.Motivos.Add(new MotivoFormulario { Activo = true });
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostPublicarAsync(CancellationToken ct)
    {
        try
        {
            var creada = await configuracion.CrearReglasAsync(
                new NuevaVersionReglas(UltimaVersionEsperada, null, Formulario.ADto()), ct);
            Exito = $"Se publicó la versión {creada.Version.Version} de las reglas operativas. Los envíos nuevos ya la usan; " +
                "los anteriores conservan las reglas con las que se admitieron.";
            return RedirectToPage();
        }
        catch (ConflictoConcurrenciaException)
        {
            Error = "Otro administrador publicó una versión mientras editabas. Recargá la página y volvé a aplicar tus cambios.";
        }
        catch (AislamientoOperadorException)
        {
            Error = "No tenés permiso para modificar estas reglas.";
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            Error = error.Message;
        }
        await CargarAsync(ct, conservarEsperada: true);
        return Page();
    }

    private async Task CargarAsync(CancellationToken ct, bool conservarEsperada = false)
    {
        Versiones = (await configuracion.ListarReglasAsync(ct)).OrderByDescending(v => v.Version).ToList();
        Vigente = await configuracion.ReglasVigentesAsync(ct);
        Base = BaseId is { } id ? await configuracion.ObtenerReglasAsync(id, ct) : Vigente;
        if (!conservarEsperada) UltimaVersionEsperada = Versiones.Count == 0 ? 0 : Versiones.Max(v => v.Version);
    }

    public string EstadoVersion(VersionConfiguracionDto v) =>
        v.Id == Vigente?.Version.Id ? "Vigente" : v.VigenteDesde > DateTime.UtcNow ? "Programada" : "Anterior";
}

public sealed class ReglasFormulario
{
    public int MaxIntentosEntrega { get; set; }
    public int PlazoEntreIntentosHoras { get; set; }
    public List<TipoPrueba> TiposPrueba { get; set; } = [];
    public int DiasEnDeposito { get; set; }
    public int PlazoMaximoDevolucionDias { get; set; }
    public bool DevolverAlAgotarIntentos { get; set; }
    public List<PlazoFormulario> Plazos { get; set; } = [];
    public List<MotivoFormulario> Motivos { get; set; } = [];
    public bool PermiteEntregaParcial { get; set; }
    public AccionBultosPendientes AccionPendientes { get; set; }
    public string? ImporteRecargoNuevaVisita { get; set; }

    // Una fila por modalidad, la ofrezca o no: así el administrador puede habilitar una que no tenía.
    public static ReglasFormulario Desde(DatosReglasDto d) => new()
    {
        MaxIntentosEntrega = d.MaxIntentosEntrega,
        PlazoEntreIntentosHoras = d.PlazoEntreIntentosHoras,
        TiposPrueba = [.. d.TiposPrueba],
        DiasEnDeposito = d.Devolucion.DiasEnDeposito,
        PlazoMaximoDevolucionDias = d.Devolucion.PlazoMaximoDevolucionDias,
        DevolverAlAgotarIntentos = d.Devolucion.DevolverAlAgotarIntentos,
        Plazos = Enum.GetValues<ModalidadServicio>().Select(m => d.Plazos.FirstOrDefault(p => p.Modalidad == m) is { } p
            ? new PlazoFormulario { Modalidad = m, Ofrecer = true, Horas = p.Horas, HoraCorte = p.HoraCorte.ToString("HH:mm") }
            : new PlazoFormulario { Modalidad = m, Horas = 48, HoraCorte = "12:00" }).ToList(),
        Motivos = d.Motivos.Select(m => new MotivoFormulario
        {
            Nombre = m.Nombre, Descripcion = m.Descripcion, Responsable = m.Responsable,
            RequiereEvidencia = m.RequiereEvidencia, EsReintentable = m.EsReintentable, Activo = m.Activo
        }).ToList(),
        PermiteEntregaParcial = d.PermiteEntregaParcial,
        AccionPendientes = d.AccionPendientes,
        ImporteRecargoNuevaVisita = Numeros.Texto(d.ImporteRecargoNuevaVisita)
    };

    public static ReglasFormulario PorDefecto() => new()
    {
        MaxIntentosEntrega = 3,
        PlazoEntreIntentosHoras = 24,
        TiposPrueba = [TipoPrueba.Firma],
        DiasEnDeposito = 7,
        PlazoMaximoDevolucionDias = 15,
        DevolverAlAgotarIntentos = true,
        Plazos = Enum.GetValues<ModalidadServicio>()
            .Select(m => new PlazoFormulario { Modalidad = m, Ofrecer = true, Horas = 48, HoraCorte = "12:00" }).ToList(),
        Motivos = [new MotivoFormulario { Nombre = "Ausente", Descripcion = "No había nadie en el domicilio",
            Responsable = Responsable.Destinatario, EsReintentable = true, Activo = true }]
    };

    public DatosReglasDto ADto()
    {
        var plazos = Plazos.Where(p => p.Ofrecer).Select(p => new PlazoServicioDto(p.Modalidad, p.Horas,
            TimeOnly.TryParseExact(p.HoraCorte, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var corte)
                ? corte
                : throw new ArgumentException($"La hora de corte de la modalidad {p.Modalidad} no es válida."))).ToList();
        var motivos = Motivos.Where(m => !m.Quitar).Select(m => new MotivoNoEntregaDto(m.Nombre?.Trim() ?? "",
            m.Descripcion?.Trim() ?? "", m.Responsable, m.RequiereEvidencia, m.EsReintentable, m.Activo)).ToList();
        return new DatosReglasDto(MaxIntentosEntrega, PlazoEntreIntentosHoras, TiposPrueba.Distinct().ToList(),
            new PoliticaDevolucionDto(DiasEnDeposito, PlazoMaximoDevolucionDias, DevolverAlAgotarIntentos),
            plazos, motivos, PermiteEntregaParcial, AccionPendientes,
            Numeros.LeerOpcional(ImporteRecargoNuevaVisita, "recargo por nueva visita"));
    }
}

public sealed class PlazoFormulario
{
    public ModalidadServicio Modalidad { get; set; }
    public bool Ofrecer { get; set; }
    public int Horas { get; set; }
    public string? HoraCorte { get; set; }
}

public sealed class MotivoFormulario
{
    public string? Nombre { get; set; }
    public string? Descripcion { get; set; }
    public Responsable Responsable { get; set; }
    public bool RequiereEvidencia { get; set; }
    public bool EsReintentable { get; set; }
    public bool Activo { get; set; }
    public bool Quitar { get; set; }
}
