namespace UltimaMilla.Domain.Envios;

public sealed class Direccion : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Direccion()
  {
    Calle = null!;
    Numero = null!;
    Localidad = null!;
    CodigoPostal = null!;
  }

  public string Calle { get; private set; }
  public string Numero { get; private set; }
  public string Localidad { get; private set; }
  public string CodigoPostal { get; private set; }
  public Destinatario? Destinatario { get; private set; }

  public Direccion(string calle, string numero, string localidad, string codigoPostal)
  {
    Calle = Validacion.Texto(calle, nameof(calle));
    Numero = Validacion.Texto(numero, nameof(numero));
    Localidad = Validacion.Texto(localidad, nameof(localidad));
    CodigoPostal = Validacion.Texto(codigoPostal, nameof(codigoPostal));
  }

  internal void Vincular(Destinatario destinatario) => Destinatario = destinatario;
}
