namespace UltimaMilla.Domain.Despacho;

// Composición y medidas de la visita, conservadas aunque el bulto cambie después.
public sealed class BultoParada : Entidad
{
  private BultoParada() { Parada = null!; Bulto = null!; }
  public Parada Parada { get; private set; }
  public Bulto Bulto { get; private set; }
  public decimal PesoKg { get; private set; }
  public decimal VolumenM3 { get; private set; }
  internal BultoParada(Parada parada, Bulto bulto)
  {
    Parada = parada;
    Bulto = bulto;
    PesoKg = bulto.Peso;
    VolumenM3 = bulto.VolumenM3;
  }
}
