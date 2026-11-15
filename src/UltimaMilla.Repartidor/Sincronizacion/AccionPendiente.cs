using UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Repartidor.Sincronizacion;

public enum EstadoAccion
{
    // Falta enviarla (o falló por red y espera su reintento).
    Pendiente,
    // El servidor la registró, ahora o en un envío anterior.
    Sincronizada,
    // El servidor la rechazó por una regla o un conflicto: no se reintenta, el repartidor la ve.
    Rechazada
}

// Elemento de la cola local. Es inmutable: cada cambio crea una copia con "with".
public sealed record AccionPendiente(
    AccionEntregaDto Accion,
    EstadoAccion Estado = EstadoAccion.Pendiente,
    int Intentos = 0,
    DateTimeOffset? ProximoIntentoEn = null,
    string? Detalle = null,
    DiagnosticoConflictoDto? Conflicto = null);
