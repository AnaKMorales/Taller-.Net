namespace UltimaMilla.Domain.Envios;

public sealed class IntentoEntrega : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private IntentoEntrega()
  {
    Envio = null!;
    Parada = null!;
  }

  private readonly List<PruebaEntrega> _pruebas = [];
  private readonly List<DetalleIntentoBulto> _detalles = [];
  public IReadOnlyCollection<DetalleIntentoBulto> Detalles => _detalles.AsReadOnly();
  public string? HuellaSolicitud { get; private set; }
  public Envio Envio { get; private set; }
  public Guid EnvioId => Envio.Id;
  public Parada Parada { get; private set; }
  public Guid ParadaId => Parada.Id;
  public int Numero { get; private set; }
  public ResultadoIntento Resultado { get; private set; }
  public MotivoNoEntrega? MotivoNoEntrega { get; private set; }
  public Guid? MotivoNoEntregaId => MotivoNoEntrega?.Id;
  public Guid IdOperacion { get; private set; }
  public DateTime OcurridoEn { get; private set; }
  public IReadOnlyCollection<PruebaEntrega> Pruebas => _pruebas.AsReadOnly();

  internal IntentoEntrega(Envio envio, Parada parada, int numero, ResultadoIntento resultado,
    MotivoNoEntrega? motivo, List<PruebaEntrega> pruebas, Guid idOperacion, DateTime ocurridoEn)
  {
    Envio = envio;
    Parada = parada;
    Numero = numero;
    Resultado = resultado;
    MotivoNoEntrega = motivo;
    _pruebas = pruebas;
    IdOperacion = idOperacion;
    OcurridoEn = ocurridoEn;
    foreach (var prueba in pruebas) prueba.Vincular(this);
  }

  internal void RegistrarDetalle(Bulto bulto, ResultadoIntento resultado, MotivoNoEntrega? motivo,
    IEnumerable<PruebaEntrega> pruebas) => _detalles.Add(new(this, bulto, resultado, motivo, pruebas));
  internal void FijarHuella(string huella) => HuellaSolicitud = huella;
  internal bool EsReintentable => _detalles.Count == 0 ? MotivoNoEntrega?.EsReintentable == true :
    _detalles.Where(d => d.Resultado == ResultadoIntento.Fallido).Any() &&
    _detalles.Where(d => d.Resultado == ResultadoIntento.Fallido).All(d => d.Motivo?.EsReintentable == true);
  internal bool TieneMotivoNoReintentable => _detalles.Count == 0 ? MotivoNoEntrega?.EsReintentable == false :
    _detalles.Any(d => d.Resultado == ResultadoIntento.Fallido && d.Motivo?.EsReintentable == false);
}
