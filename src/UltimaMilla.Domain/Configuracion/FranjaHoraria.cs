namespace UltimaMilla.Domain.Configuracion;

public sealed class FranjaHoraria : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private FranjaHoraria()
  {
    Nombre = null!;
  }

  public string Nombre { get; private set; }
  public TimeOnly Desde { get; private set; }
  public TimeOnly Hasta { get; private set; }
  public ZonaCobertura? Zona { get; private set; }
  public Guid? ZonaId => Zona?.Id;

  public FranjaHoraria(string nombre, TimeOnly desde, TimeOnly hasta)
  {
    Nombre = Validacion.Texto(nombre, nameof(nombre));
    if (hasta <= desde) throw new ArgumentException("La franja debe finalizar despues de su inicio.", nameof(hasta));
    Desde = desde;
    Hasta = hasta;
  }

  internal void Vincular(ZonaCobertura zona) => Zona = zona;
}
