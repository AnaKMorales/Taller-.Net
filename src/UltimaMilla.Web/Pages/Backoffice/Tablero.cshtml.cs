using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Web.Compartido;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice;

[Authorize(Policy = Politicas.Backoffice)]
public sealed class TableroModel(IConsultasBackoffice consultas, IWebHostEnvironment entorno) : PageModel
{
    public const int CambiosVisibles = 20;

    public ResumenTablero Resumen { get; private set; } = new(new Dictionary<EstadoEnvio, int>(), []);

    public bool Simulacion => entorno.IsDevelopment();

    public string TextosJson => JsonSerializer.Serialize(new
    {
        estados = Textos.Estados.ToDictionary(p => p.Key.ToString(), p => p.Value),
        eventos = Textos.Eventos.ToDictionary(p => p.Key.ToString(), p => p.Value)
    });

    // Margen para considerar que un envío está en riesgo de no cumplir su ventana.
    public static readonly TimeSpan MargenRiesgo = TimeSpan.FromHours(2);
    // Una posición más vieja que esto ya no describe dónde está el repartidor.
    public static readonly TimeSpan AntiguedadFlota = TimeSpan.FromHours(12);

    public PanelRiesgo Panel { get; private set; } = PanelRiesgo.Vacio;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Resumen = await consultas.TableroAsync(CambiosVisibles, cancellationToken);
        Panel = await CargarPanelAsync(cancellationToken);
    }

    // Lo pide tablero.js para refrescar los envíos en riesgo y la flota sin recargar la página.
    public async Task<PartialViewResult> OnGetPanelAsync(CancellationToken cancellationToken) =>
        Partial("_PanelRiesgo", await CargarPanelAsync(cancellationToken));

    private async Task<PanelRiesgo> CargarPanelAsync(CancellationToken cancellationToken) => new(
        await consultas.EnviosEnRiesgoAsync(MargenRiesgo, cancellationToken),
        await consultas.UltimasPosicionesAsync(AntiguedadFlota, cancellationToken),
        (int)MargenRiesgo.TotalHours, (int)AntiguedadFlota.TotalHours, DateTime.UtcNow);

    public int Cantidad(EstadoEnvio estado) => Resumen.EnviosPorEstado.GetValueOrDefault(estado);
}

public sealed record PanelRiesgo(IReadOnlyList<EnvioEnRiesgoDto> Riesgo, IReadOnlyList<PosicionRepartidorDto> Flota,
    int MargenHoras, int FlotaHoras, DateTime GeneradoEn)
{
    public static readonly PanelRiesgo Vacio = new([], [], 0, 0, DateTime.UtcNow);
}
