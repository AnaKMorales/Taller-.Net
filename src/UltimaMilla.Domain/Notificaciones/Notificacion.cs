namespace UltimaMilla.Domain.Notificaciones;

public sealed class Notificacion : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Notificacion()
  {
    Destinatario = null!;
    Mensaje = null!;
  }

  public Destinatario Destinatario { get; private set; }
  public Guid DestinatarioId => Destinatario.Id;
  public CanalNotificacion Canal { get; private set; }
  public string Mensaje { get; private set; }
  public DateTime? EnviadaEn { get; private set; }

  public Notificacion(Destinatario destinatario, CanalNotificacion canal, string mensaje)
  {
    Destinatario = Validacion.Requerido(destinatario, nameof(destinatario));
    if (destinatario.Envio is null) throw new InvalidOperationException("El destinatario debe pertenecer a un envio.");
    Canal = Validacion.Enumeracion(canal, nameof(canal));
    Mensaje = Validacion.Texto(mensaje, nameof(mensaje));
    destinatario.Registrar(this);
  }

  public void MarcarEnviada(DateTime enviadaEn)
  {
    Validacion.Fecha(enviadaEn, nameof(enviadaEn));
    if (enviadaEn < Destinatario.Envio!.FechaAdmision) throw new ArgumentOutOfRangeException(nameof(enviadaEn));
    if (EnviadaEn is not null) throw new InvalidOperationException("La notificacion ya fue enviada.");
    EnviadaEn = enviadaEn;
  }
}
