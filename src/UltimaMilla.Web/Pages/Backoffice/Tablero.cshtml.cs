using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Web.Backoffice;
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

    public async Task OnGetAsync(CancellationToken cancellationToken) =>
        Resumen = await consultas.TableroAsync(CambiosVisibles, cancellationToken);

    public int Cantidad(EstadoEnvio estado) => Resumen.EnviosPorEstado.GetValueOrDefault(estado);
}
