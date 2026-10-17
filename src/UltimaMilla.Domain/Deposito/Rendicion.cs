namespace UltimaMilla.Domain.Deposito;

public sealed class Rendicion : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Rendicion()
  {
    Ruta = null!;
  }

  public Ruta Ruta { get; private set; }
  public Guid RutaId => Ruta.Id;
  public DateTime FechaHora { get; private set; }

  internal Rendicion(Ruta ruta, DateTime fechaHora)
  {
    Ruta = ruta;
    FechaHora = fechaHora;
  }
}
