using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Portal;

public enum EstadoAviso
{
    Pendiente,
    Entregado,
    Fallido
}

public sealed record SuscripcionDto(Guid Id, string Destino, IReadOnlyCollection<TipoEvento> Eventos, bool Activa,
    int Entregados, int Fallidos, int Pendientes);

public sealed record AvisoDto(Guid Id, string CodigoSeguimiento, TipoEvento Evento, EstadoAviso Estado, int Intentos,
    DateTime OcurridoEn, DateTime? UltimoIntentoEn);

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

    // false si la suscripción no existe o es de otra cuenta.
    Task<bool> DesactivarSuscripcionAsync(Guid id, CancellationToken cancellationToken = default);
}
