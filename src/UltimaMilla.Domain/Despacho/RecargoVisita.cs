namespace UltimaMilla.Domain.Despacho;

// Registro económico devengado al despachar; no representa un pago recibido.
public sealed class RecargoVisita : Entidad
{
  private RecargoVisita() { Envio = null!; Parada = null!; }
  public Envio Envio { get; private set; }
  public Parada Parada { get; private set; }
  public decimal Importe { get; private set; }
  public DateTime RegistradoEn { get; private set; }
  internal RecargoVisita(Envio envio, Parada parada, DateTime fecha)
  {
    Envio = envio;
    Parada = parada;
    Importe = parada.RecargoPrevisto;
    RegistradoEn = fecha;
  }
}
