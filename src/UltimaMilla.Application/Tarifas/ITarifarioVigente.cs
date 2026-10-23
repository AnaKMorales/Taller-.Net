using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tarifas;

public sealed record AjusteTarifaDto(TipoAjuste Tipo, decimal ValorPorcentaje);

public sealed record LineaTarifaDto(Guid ZonaId, ModalidadServicio Modalidad, decimal PesoDesde, decimal PesoHasta,
    decimal VolumenHasta, decimal Precio, IReadOnlyList<AjusteTarifaDto> Ajustes);

// ProximaVigencia es la fecha en que entra en vigor otro cuadro ya cargado; la caché no debe durar más que eso.
public sealed record TarifarioDto(Guid CuadroId, int Version, DateTime VigenteDesde, DateTime? ProximaVigencia,
    IReadOnlyList<LineaTarifaDto> Lineas);

public interface ITarifarioVigente
{
    // Devuelve el cuadro tarifario vigente del operador, o null si todavía no tiene uno.
    Task<TarifarioDto?> ObtenerAsync(Guid operadorId, CancellationToken cancellationToken = default);
}

public interface IInvalidacionTarifario
{
    // Se llama al crear o activar una versión nueva del tarifario del operador.
    Task InvalidarAsync(Guid operadorId, CancellationToken cancellationToken = default);
}
