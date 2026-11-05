using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice;

[Authorize(Policy = Politicas.Backoffice)]
public sealed class FallidosModel(IConsultasBackoffice consultas, IColaFallidos cola, ITenantActual tenant,
    ILogger<FallidosModel> logger) : PageModel
{
    public IReadOnlyList<AvisoFallidoDto> Avisos { get; private set; } = [];
    public IReadOnlyList<MensajeFallido> Muertos { get; private set; } = [];
    public bool ColaConfigurada => cola.Configurada;
    public string? ErrorCola { get; private set; }

    [TempData] public string? Resultado { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Avisos = await consultas.AvisosFallidosAsync(cancellationToken);
        if (!cola.Configurada) return;
        try { Muertos = await cola.ListarAsync(Operador(), cancellationToken: cancellationToken); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // El broker caído no debe tumbar la pantalla de avisos.
            logger.LogError(error, "No se pudo leer la cola de fallidos.");
            ErrorCola = "No se pudo consultar la cola de fallidos. Probá de nuevo en un momento.";
        }
    }

    // POST protegido con antiforgery (Razor Pages lo valida). El operador sale de la sesión, nunca del formulario.
    public async Task<IActionResult> OnPostReintentarAsync(string mensajeId, CancellationToken cancellationToken)
    {
        try
        {
            Resultado = await cola.ReintentarAsync(Operador(), mensajeId, cancellationToken)
                ? "El mensaje volvió a la cola de avisos y se va a procesar de nuevo."
                : "No se encontró el mensaje (quizás ya lo reintentó otra persona).";
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "No se pudo reintentar el mensaje {MensajeId}.", mensajeId);
            Resultado = "No se pudo reintentar el mensaje. Probá de nuevo en un momento.";
        }
        return RedirectToPage();
    }

    private Guid Operador() => tenant.Contexto?.OperadorId ?? throw new InvalidOperationException("Sin operador en la sesión.");
}
