namespace UltimaMilla.Domain.Deposito;

public sealed class BultoDevolucion : Entidad
{
  private BultoDevolucion() { Devolucion = null!; Bulto = null!; }
  public Devolucion Devolucion { get; private set; }
  public Bulto Bulto { get; private set; }
  internal BultoDevolucion(Devolucion devolucion, Bulto bulto)
  { Devolucion = devolucion; Bulto = bulto; }
}
