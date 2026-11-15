using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Portal;

public enum EstadoAviso
{
    Pendiente,
    Entregado,
    Fallido
}

public sealed record SuscripcionDto(Guid Id, string Destino, IReadOnlyCollection<TipoEvento> Eventos, bool Activa,
    int Entregados, int Fallidos, int Pendientes, int VersionContrato = 1);

public sealed record AvisoDto(Guid Id, string CodigoSeguimiento, TipoEvento Evento, EstadoAviso Estado, int Intentos,
    DateTime OcurridoEn, DateTime? UltimoIntentoEn);

// Lo necesario para volver a publicar el evento de un aviso. Evento es el contrato V1 que ya reciben
// los comercios; su MensajeId lo reemplaza el caso de uso por uno nuevo.
public sealed record AvisoReenviable(bool Entregado, EventoEnvioV1 Evento);

// El secreto se muestra una sola vez, al crear la suscripción.
public sealed record SuscripcionCreada(Guid Id, string Secreto);

// Panel de avisos del comercio: solo ve los de su cuenta comercial.
public interface IPanelAvisos
{
    Task<IReadOnlyList<SuscripcionDto>> SuscripcionesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvisoDto>> UltimosAvisosAsync(int cantidad, CancellationToken cancellationToken = default);

    // Lanza ArgumentException si la URL o los eventos no son válidos.
    Task<SuscripcionCreada> CrearSuscripcionAsync(Uri destino, IReadOnlyCollection<TipoEvento> eventos,
        CancellationToken cancellationToken = default);

    Task<SuscripcionCreada> CrearSuscripcionAsync(Uri destino, IReadOnlyCollection<TipoEvento> eventos,
        int versionContrato, CancellationToken cancellationToken = default) => versionContrato == 1
        ? CrearSuscripcionAsync(destino, eventos, cancellationToken)
        : throw new NotSupportedException("Este adaptador no admite webhooks V2.");

    // false si la suscripción no existe o es de otra cuenta.
    Task<bool> DesactivarSuscripcionAsync(Guid id, CancellationToken cancellationToken = default);

    // null si el aviso no existe o es de otra cuenta.
    Task<AvisoReenviable?> AvisoParaReenviarAsync(Guid avisoId, CancellationToken cancellationToken = default);
}
