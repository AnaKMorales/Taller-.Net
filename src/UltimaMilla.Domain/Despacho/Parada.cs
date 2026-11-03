namespace UltimaMilla.Domain.Despacho;

public sealed class Parada : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Parada()
  {
    Ruta = null!;
    Envio = null!;
  }

  private readonly List<IntentoEntrega> _intentos = [];
  private readonly List<BultoParada> _bultos = [];
  public IReadOnlyCollection<BultoParada> Bultos => _bultos.AsReadOnly();
  public decimal PesoTotalKg => Carga().Sum(b => b.PesoKg);
  public decimal VolumenTotalM3 => Carga().Sum(b => b.VolumenM3);
  public Ruta Ruta { get; private set; }
  public Guid RutaId => Ruta.Id;
  public Envio Envio { get; private set; }
  public Guid EnvioId => Envio.Id;
  public int Cod { get; private set; }
  public DateTime HoraEstimadaLlegada { get; private set; }
  public bool Activa { get; private set; } = true;
  public decimal RecargoPrevisto { get; private set; }
  public RecargoVisita? Recargo { get; private set; }
  public IReadOnlyCollection<IntentoEntrega> Intentos => _intentos.AsReadOnly();

  internal Parada(Ruta ruta, Envio envio, int cod, DateTime horaEstimadaLlegada, IEnumerable<Bulto> bultos)
  {
    Ruta = ruta;
    Envio = envio;
    Cod = cod;
    HoraEstimadaLlegada = horaEstimadaLlegada;
    RecargoPrevisto = envio.RecargoParaNuevaVisita();
    foreach (var bulto in bultos) _bultos.Add(new(this, bulto));
  }

  private IReadOnlyCollection<BultoParada> Carga() => _bultos.Count > 0 ? _bultos :
    throw new InvalidOperationException("Carga la composición de bultos de la parada antes de calcular su capacidad.");

  internal void Finalizar() => Activa = false;
  internal void ConfirmarRecargo(RecargoVisita recargo) => Recargo = recargo;
  internal void Registrar(IntentoEntrega intento) => _intentos.Add(intento);
}
