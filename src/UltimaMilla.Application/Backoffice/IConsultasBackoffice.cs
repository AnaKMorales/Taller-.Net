using UltimaMilla.Application.Tablero;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Backoffice;

public sealed record ResumenTablero(IReadOnlyDictionary<EstadoEnvio, int> EnviosPorEstado,
    IReadOnlyList<CambioEnvio> UltimosCambios);

public sealed record IncidenciaDto(Guid EnvioId, string Destinatario, EstadoEnvio Estado, int IntentosFallidos,
    string? UltimoMotivo, int Discrepancias, DateTime UltimoEvento);

public sealed record AvisoFallidoDto(Guid EntregaId, Guid EnvioId, string Comercio, string Destino,
    TipoEvento Evento, int Intentos, DateTime? UltimoIntentoEn);

// Envío que todavía no se entregó y cuya ventana comprometida ya venció o vence dentro del margen.
public sealed record EnvioEnRiesgoDto(Guid EnvioId, string CodigoSeguimiento, string Destinatario, string Zona,
    EstadoEnvio Estado, DateTimeOffset VentanaHasta, bool Vencido, string? Repartidor);

// Última posición conocida de un repartidor. Hoy sale de los eventos que registra la app con posición
// (intentos de entrega): no es un seguimiento continuo del vehículo.
public sealed record PosicionRepartidorDto(string Repartidor, decimal Latitud, decimal Longitud, DateTime ReportadaEn,
    string? Vehiculo = null);

public sealed record EnvioPorRecibirDto(Guid EnvioId, string CodigoSeguimiento, string Comercio, string Destinatario,
    int Bultos, DateTime FechaAdmision);

// Solo devuelven datos del operador del usuario.
public interface IConsultasBackoffice
{
    Task<ResumenTablero> TableroAsync(int ultimosCambios, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EnvioEnRiesgoDto>> EnviosEnRiesgoAsync(TimeSpan margen, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PosicionRepartidorDto>> UltimasPosicionesAsync(TimeSpan antiguedadMaxima,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IncidenciaDto>> IncidenciasAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvisoFallidoDto>> AvisosFallidosAsync(CancellationToken cancellationToken = default);
    // Admitidos que todavía no llegaron al depósito
    Task<IReadOnlyList<EnvioPorRecibirDto>> PorRecibirAsync(CancellationToken cancellationToken = default);
}
