namespace UltimaMilla.Application.Mensajeria;

// El adaptador debe completar la tarea únicamente cuando el broker confirme la publicación
// y la ruta de entrega sea válida. Un nack, mensaje no enrutable o falta de confirmación falla.
// Debe respetar la cancelación y propagar MensajeId como identificador del transporte.
public interface ITransporteMensajes
{
    Task PublicarConfirmadoAsync(MensajeParaPublicar mensaje, CancellationToken cancellationToken = default);
}

// Instantánea sin entidades de EF; el transporte no recibe acceso al contexto ni al negocio.
public sealed record MensajeParaPublicar(Guid MensajeId, Guid OperadorId, string Tipo,
    string Contenido, DateTime CreadoEn, string? TraceParent = null, string? TraceState = null);
