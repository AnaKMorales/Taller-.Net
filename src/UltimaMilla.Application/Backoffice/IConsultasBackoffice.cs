using UltimaMilla.Application.Tablero;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Backoffice;

public sealed record ResumenTablero(IReadOnlyDictionary<EstadoEnvio, int> EnviosPorEstado,
    IReadOnlyList<CambioEnvio> UltimosCambios);

// Envío que necesita atención del operador: no se entregó, se devuelve, se perdió o llegó distinto a lo declarado.
public sealed record IncidenciaDto(Guid EnvioId, string Destinatario, EstadoEnvio Estado, int IntentosFallidos,
    string? UltimoMotivo, int Discrepancias, DateTime UltimoEvento);

// Aviso a un comercio (webhook) que no se pudo entregar. Destino es solo el host, sin ruta ni parámetros.
public sealed record AvisoFallidoDto(Guid EntregaId, Guid EnvioId, string Comercio, string Destino,
    TipoEvento Evento, int Intentos, DateTime? UltimoIntentoEn);

// Consultas del backoffice. Siempre devuelven solo datos del operador del usuario que pregunta.
public interface IConsultasBackoffice
{
    Task<ResumenTablero> TableroAsync(int ultimosCambios, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidenciaDto>> IncidenciasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvisoFallidoDto>> AvisosFallidosAsync(CancellationToken cancellationToken = default);
}
