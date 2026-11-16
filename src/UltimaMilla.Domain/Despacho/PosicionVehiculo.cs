namespace UltimaMilla.Domain.Despacho;

// Lectura periódica del GPS del dispositivo del repartidor durante la jornada (req. 23 y mapa de flota, req. 29).
// No es un evento de envío: no cambia estados ni genera outbox. Pertenece a una ruta despachada del mismo
// repartidor. La hora es la del dispositivo (la lectura puede viajar horas en la cola offline); RecibidaEn es la
// del servidor. IdLectura la genera el dispositivo: reenviar el mismo lote no duplica filas.
public sealed class PosicionVehiculo : Entidad
{
  // Más vieja que esto no sirve para el mapa ni para reconstruir el recorrido del día.
  public static readonly TimeSpan AntiguedadMaxima = TimeSpan.FromHours(24);
  // Diferencia aceptada entre el reloj del dispositivo y el del servidor.
  public static readonly TimeSpan ToleranciaReloj = TimeSpan.FromMinutes(5);
  public const decimal PrecisionMaximaMetros = 10_000;

  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private PosicionVehiculo()
  {
    Ruta = null!;
    Punto = null!;
  }

  public Ruta Ruta { get; private set; }
  public Guid RutaId => Ruta.Id;
  public Guid IdLectura { get; private set; }
  public GeoPunto Punto { get; private set; }
  public decimal? PrecisionMetros { get; private set; }
  public DateTime RegistradaEn { get; private set; }
  public DateTime RecibidaEn { get; private set; }

  public PosicionVehiculo(Ruta ruta, Repartidor repartidor, Guid idLectura, GeoPunto punto, decimal? precisionMetros,
    DateTime registradaEn, DateTime recibidaEn)
  {
    Ruta = Validacion.Requerido(ruta, nameof(ruta));
    Validacion.Requerido(repartidor, nameof(repartidor));
    Punto = Validacion.Requerido(punto, nameof(punto));
    if (idLectura == Guid.Empty) throw new ArgumentException("La lectura necesita un identificador.", nameof(idLectura));
    Validacion.Fecha(registradaEn, nameof(registradaEn));
    Validacion.Fecha(recibidaEn, nameof(recibidaEn));
    if (registradaEn.Kind != DateTimeKind.Utc || recibidaEn.Kind != DateTimeKind.Utc)
      throw new ArgumentException("Las horas de la lectura deben expresarse en UTC.");
    if (ruta.RepartidorId != repartidor.Id)
      throw new InvalidOperationException("La ruta pertenece a otro repartidor.");
    if (ruta.DespachadaEn is not { } despachadaEn)
      throw new InvalidOperationException("La ruta todavía no fue despachada.");
    if (registradaEn < despachadaEn - ToleranciaReloj)
      throw new ArgumentOutOfRangeException(nameof(registradaEn), "La lectura es anterior al despacho de la ruta.");
    if (ruta.Rendicion is { } rendicion && registradaEn > rendicion.FechaHora + ToleranciaReloj)
      throw new ArgumentOutOfRangeException(nameof(registradaEn), "La lectura es posterior a la rendición de la ruta.");
    if (registradaEn > recibidaEn + ToleranciaReloj)
      throw new ArgumentOutOfRangeException(nameof(registradaEn), "La lectura está en el futuro.");
    if (recibidaEn - registradaEn > AntiguedadMaxima)
      throw new ArgumentOutOfRangeException(nameof(registradaEn), "La lectura es demasiado antigua.");
    if (precisionMetros is < 0 or > PrecisionMaximaMetros)
      throw new ArgumentOutOfRangeException(nameof(precisionMetros));

    IdLectura = idLectura;
    PrecisionMetros = precisionMetros;
    RegistradaEn = registradaEn;
    RecibidaEn = recibidaEn;
  }
}
