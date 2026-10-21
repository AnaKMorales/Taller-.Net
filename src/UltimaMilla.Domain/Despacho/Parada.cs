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
  public Ruta Ruta { get; private set; }
  public Guid RutaId => Ruta.Id;
  public Envio Envio { get; private set; }
  public Guid EnvioId => Envio.Id;
  public int Cod { get; private set; }
  public DateTime HoraEstimadaLlegada { get; private set; }
  public bool Activa { get; private set; } = true;
  public IReadOnlyCollection<IntentoEntrega> Intentos => _intentos.AsReadOnly();

  internal Parada(Ruta ruta, Envio envio, int cod, DateTime horaEstimadaLlegada)
  {
    Ruta = ruta;
    Envio = envio;
    Cod = cod;
    HoraEstimadaLlegada = horaEstimadaLlegada;
  }

  internal void Finalizar() => Activa = false;
  internal void Registrar(IntentoEntrega intento) => _intentos.Add(intento);
}
