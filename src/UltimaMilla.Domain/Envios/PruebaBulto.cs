namespace UltimaMilla.Domain.Envios;

// Una evidencia puede acreditar varios resultados del mismo intento.
public sealed class PruebaBulto : Entidad
{
  private PruebaBulto() { Detalle = null!; Prueba = null!; }
  public DetalleIntentoBulto Detalle { get; private set; }
  public PruebaEntrega Prueba { get; private set; }
  internal PruebaBulto(DetalleIntentoBulto detalle, PruebaEntrega prueba)
  { Detalle = detalle; Prueba = prueba; }
}
