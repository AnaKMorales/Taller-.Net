using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.RegistrarIntento;

public enum MotivoConflictoBulto
{
    EstadoCambio,
    ParadaCerrada,
    FueraDeParada,
    SinCambioDeEstado
}

public sealed record BultoEnConflictoDto(Guid BultoId, string? Codigo, EstadoBulto? EstadoActual, MotivoConflictoBulto Motivo);

public sealed record ConflictoIntentoDto(Guid ParadaId, Guid VersionActualEnvio, bool ParadaActiva,
    IReadOnlyList<BultoEnConflictoDto> Bultos);

public sealed class ConflictoIntentoException(ConflictoIntentoDto conflicto, Exception innerException)
    : ConflictoConcurrenciaException(innerException)
{
    public ConflictoIntentoDto Conflicto { get; } = conflicto;
}
