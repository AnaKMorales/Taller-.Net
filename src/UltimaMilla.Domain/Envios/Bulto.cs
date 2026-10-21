namespace UltimaMilla.Domain.Envios;

public sealed class Bulto : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Bulto()
  {
    Codigo = null!;
  }

  private readonly List<EscaneoBulto> _escaneos = [];
  private readonly List<Discrepancia> _discrepancias = [];
  public IReadOnlyCollection<EscaneoBulto> Escaneos => _escaneos.AsReadOnly();
  public IReadOnlyCollection<Discrepancia> Discrepancias => _discrepancias.AsReadOnly();
  public string Codigo { get; private set; }
  public decimal Peso { get; private set; }
  // Peso en kg y dimensiones en cm.
  public decimal Alto { get; private set; }
  public decimal Ancho { get; private set; }
  public decimal Largo { get; private set; }
  public decimal VolumenM3 => Alto * Ancho * Largo / 1_000_000m;
  public Envio? Envio { get; private set; }
  public Guid? EnvioId => Envio?.Id;

  public Bulto(string codigo, decimal peso, decimal alto, decimal ancho, decimal largo)
  {
    Codigo = Validacion.Texto(codigo, nameof(codigo));
    Peso = Validacion.Positivo(peso, nameof(peso));
    Alto = Validacion.Positivo(alto, nameof(alto));
    Ancho = Validacion.Positivo(ancho, nameof(ancho));
    Largo = Validacion.Positivo(largo, nameof(largo));
  }

  internal void Vincular(Envio envio) => Envio = envio;
  internal void Registrar(EscaneoBulto escaneo) => _escaneos.Add(escaneo);
  internal void Registrar(Discrepancia discrepancia) => _discrepancias.Add(discrepancia);
}
