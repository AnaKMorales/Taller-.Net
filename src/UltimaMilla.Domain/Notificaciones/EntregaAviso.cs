namespace UltimaMilla.Domain.Notificaciones;

public sealed class EntregaAviso : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private EntregaAviso()
  {
    Suscripcion = null!;
    Evento = null!;
  }

  public SuscripcionAviso Suscripcion { get; private set; }
  public Guid SuscripcionId => Suscripcion.Id;
  public EventoDeEnvio Evento { get; private set; }
  public Guid EventoId => Evento.Id;
  public bool Exitoso { get; private set; }
  public int Intentos { get; private set; }
  public DateTime? UltimoIntentoEn { get; private set; }

  public EntregaAviso(SuscripcionAviso suscripcion, EventoDeEnvio evento)
  {
    Suscripcion = Validacion.Requerido(suscripcion, nameof(suscripcion));
    Evento = Validacion.Requerido(evento, nameof(evento));
    if (!suscripcion.Activa || !suscripcion.TiposEvento.Contains(evento.Tipo))
      throw new InvalidOperationException("La suscripcion no esta activa para este evento.");
    if (suscripcion.CuentaComercialId != evento.Envio.CuentaComercialId)
      throw new InvalidOperationException("El evento pertenece a otra cuenta comercial.");
    suscripcion.Registrar(this);
  }

  public void RegistrarIntento(bool exitoso, DateTime ocurridoEn)
  {
    Validacion.Fecha(ocurridoEn, nameof(ocurridoEn));
    if (ocurridoEn < Evento.OcurridoEn || (UltimoIntentoEn is not null && ocurridoEn < UltimoIntentoEn.Value))
      throw new ArgumentOutOfRangeException(nameof(ocurridoEn));
    if (Exitoso) throw new InvalidOperationException("El aviso ya se entrego.");
    Intentos++;
    UltimoIntentoEn = ocurridoEn;
    Exitoso = exitoso;
  }
}
