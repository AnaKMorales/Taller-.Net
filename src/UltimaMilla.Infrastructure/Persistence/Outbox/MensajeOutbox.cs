using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Infrastructure.Persistence.Outbox;

// Registro técnico de infraestructura; no es una entidad del negocio.
public sealed class MensajeOutbox
{
    private MensajeOutbox() { Evento = null!; Tipo = null!; Contenido = null!; }

    internal MensajeOutbox(EventoDeEnvio evento, string contenido, string tipo = EventoEnvioV1.TipoMensaje)
    {
        Id = evento.Id; // Estable también si se vuelve a intentar el mismo guardado.
        Evento = evento;
        Tipo = tipo;
        Contenido = contenido;
        CreadoEn = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public EventoDeEnvio Evento { get; private set; }
    public string Tipo { get; private set; }
    public string Contenido { get; private set; }
    public DateTime CreadoEn { get; private set; }
    public DateTime? PublicadoEn { get; private set; }

    internal void MarcarPublicado(DateTime fechaUtc)
    {
        if (fechaUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("La fecha debe ser UTC.", nameof(fechaUtc));
        PublicadoEn ??= fechaUtc;
    }
}
