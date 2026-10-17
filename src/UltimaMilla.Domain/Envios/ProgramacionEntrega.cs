namespace UltimaMilla.Domain.Envios;

// Fotografia de la franja elegida y sus fechas concretas. Application resuelve los offsets horarios.
public sealed record ProgramacionEntrega
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private ProgramacionEntrega()
  {
    Franja = null!;
  }

  public FranjaHoraria Franja { get; private set; }
  public Guid FranjaId => Franja.Id;
  public DateOnly FechaEntrega { get; private set; }
  public DateTimeOffset InicioLocal { get; private set; }
  public DateTimeOffset FinLocal { get; private set; }
  public DateTime VentanaDesde => InicioLocal.UtcDateTime;
  public DateTime VentanaHasta => FinLocal.UtcDateTime;

  public ProgramacionEntrega(FranjaHoraria franja, DateOnly fechaEntrega,
    DateTimeOffset inicioLocal, DateTimeOffset finLocal)
  {
    Franja = Validacion.Requerido(franja, nameof(franja));
    if (franja.Zona is null) throw new ArgumentException("La franja debe pertenecer a una zona.", nameof(franja));
    if (fechaEntrega == default) throw new ArgumentException("La fecha de entrega es obligatoria.", nameof(fechaEntrega));
    if (inicioLocal.DateTime != fechaEntrega.ToDateTime(franja.Desde) ||
        finLocal.DateTime != fechaEntrega.ToDateTime(franja.Hasta) || finLocal <= inicioLocal)
      throw new ArgumentException("La ventana debe corresponder exactamente a la fecha y franja elegidas.");

    FechaEntrega = fechaEntrega;
    InicioLocal = inicioLocal;
    FinLocal = finLocal;
  }

  public static ProgramacionEntrega Crear(FranjaHoraria franja, DateOnly fechaEntrega, TimeSpan desfaseUtc)
  {
    ArgumentNullException.ThrowIfNull(franja);
    return new ProgramacionEntrega(franja, fechaEntrega,
      new DateTimeOffset(fechaEntrega.ToDateTime(franja.Desde), desfaseUtc),
      new DateTimeOffset(fechaEntrega.ToDateTime(franja.Hasta), desfaseUtc));
  }
}
