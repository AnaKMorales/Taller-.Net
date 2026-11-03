namespace UltimaMilla.Domain.Deposito;

public sealed class Devolucion : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Devolucion()
  {
    Envio = null!;
  }

  public Envio Envio { get; private set; }
  private readonly List<BultoDevolucion> _bultos = [];
  public IReadOnlyCollection<BultoDevolucion> Bultos => _bultos.AsReadOnly();
  public Guid EnvioId => Envio.Id;
  public DateTime FechaInicio { get; private set; }
  public DateTime? FechaCierre { get; private set; }
  public EstadoDevolucion Estado { get; private set; } = EstadoDevolucion.EnProceso;
  public DateTime PlazoLimite { get; private set; }

  internal Devolucion(Envio envio, DateTime fechaInicio, DateTime plazoLimite, IEnumerable<Bulto> bultos)
  {
    Envio = envio;
    FechaInicio = fechaInicio;
    PlazoLimite = plazoLimite;
    foreach (var bulto in bultos) _bultos.Add(new(this, bulto));
  }

  public void Completar(Usuario responsable, DateTime fechaCierre)
  {
    if (Estado == EstadoDevolucion.Completado) throw new InvalidOperationException("La devolucion ya esta completada.");
    if (fechaCierre < FechaInicio) throw new ArgumentOutOfRangeException(nameof(fechaCierre));
    if (_bultos.Count == 0) throw new InvalidOperationException("Carga los bultos incluidos en la devolución.");
    if (_bultos.Any(b => !b.Bulto.Escaneos.Any(e => e.Tipo == TipoEscaneo.Devolucion && e.DevolucionId == Id &&
        e.FechaHora >= FechaInicio && e.FechaHora <= fechaCierre)))
      throw new InvalidOperationException("Falta escanear un bulto de la devolucion.");
    Envio.ConfirmarDevolucion(this, responsable, fechaCierre);
    FechaCierre = fechaCierre;
    Estado = EstadoDevolucion.Completado;
  }
}
