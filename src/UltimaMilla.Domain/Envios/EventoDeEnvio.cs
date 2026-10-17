namespace UltimaMilla.Domain.Envios;

// Registro historico inmutable. Solo Envio crea eventos al aplicar una transicion.
public sealed class EventoDeEnvio : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private EventoDeEnvio()
  {
    Envio = null!;
    Responsable = null!;
  }

  public Envio Envio { get; private set; }
  public Guid EnvioId => Envio.Id;
  public Guid OperadorId => Envio.OperadorId;
  public TipoEvento Tipo { get; private set; }
  public OrigenEvento Origen { get; private set; }
  public EstadoEnvio? EstadoAnterior { get; private set; }
  public EstadoEnvio EstadoNuevo { get; private set; }
  public Usuario Responsable { get; private set; }
  public Guid ResponsableId => Responsable.Id;
  public DateTime OcurridoEn { get; private set; }
  public GeoPunto? Posicion { get; private set; }
  public string? Detalle { get; private set; }
  public Guid? IdOperacion { get; private set; }
  public ProgramacionEntrega? ProgramacionAnterior { get; private set; }
  public ProgramacionEntrega? ProgramacionNueva { get; private set; }

  internal EventoDeEnvio(Envio envio, TipoEvento tipo, OrigenEvento origen, EstadoEnvio? estadoAnterior,
    EstadoEnvio estadoNuevo, Usuario responsable, DateTime ocurridoEn, GeoPunto? posicion,
    string? detalle, Guid? idOperacion,
    ProgramacionEntrega? programacionAnterior = null, ProgramacionEntrega? programacionNueva = null)
  {
    Envio = envio;
    Tipo = tipo;
    Origen = origen;
    EstadoAnterior = estadoAnterior;
    EstadoNuevo = estadoNuevo;
    Responsable = responsable;
    OcurridoEn = ocurridoEn;
    Posicion = posicion;
    Detalle = detalle;
    IdOperacion = idOperacion;
    ProgramacionAnterior = programacionAnterior;
    ProgramacionNueva = programacionNueva;
  }
}
