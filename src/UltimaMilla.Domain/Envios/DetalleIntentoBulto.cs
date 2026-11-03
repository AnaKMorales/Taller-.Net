namespace UltimaMilla.Domain.Envios;

public sealed class DetalleIntentoBulto : Entidad
{
  private DetalleIntentoBulto() { Intento = null!; Bulto = null!; }
  private readonly List<PruebaBulto> _evidencias = [];
  public IntentoEntrega Intento { get; private set; }
  public Bulto Bulto { get; private set; }
  public ResultadoIntento Resultado { get; private set; }
  public MotivoNoEntrega? Motivo { get; private set; }
  public IReadOnlyCollection<PruebaBulto> Evidencias => _evidencias.AsReadOnly();

  internal DetalleIntentoBulto(IntentoEntrega intento, Bulto bulto, ResultadoIntento resultado,
    MotivoNoEntrega? motivo, IEnumerable<PruebaEntrega> pruebas)
  {
    Intento = intento;
    Bulto = bulto;
    Resultado = resultado;
    Motivo = motivo;
    foreach (var prueba in pruebas) _evidencias.Add(new PruebaBulto(this, prueba));
  }
}
