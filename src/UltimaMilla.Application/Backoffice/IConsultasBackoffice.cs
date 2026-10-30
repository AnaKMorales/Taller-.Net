using UltimaMilla.Application.Tablero;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Backoffice;

public sealed record ResumenTablero(IReadOnlyDictionary<EstadoEnvio, int> EnviosPorEstado,
    IReadOnlyList<CambioEnvio> UltimosCambios);

public sealed record IncidenciaDto(Guid EnvioId, string Destinatario, EstadoEnvio Estado, int IntentosFallidos,
    string? UltimoMotivo, int Discrepancias, DateTime UltimoEvento);

public sealed record AvisoFallidoDto(Guid EntregaId, Guid EnvioId, string Comercio, string Destino,
    TipoEvento Evento, int Intentos, DateTime? UltimoIntentoEn);

// Solo devuelven datos del operador del usuario.
public interface IConsultasBackoffice
{
    Task<ResumenTablero> TableroAsync(int ultimosCambios, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidenciaDto>> IncidenciasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvisoFallidoDto>> AvisosFallidosAsync(CancellationToken cancellationToken = default);
}
