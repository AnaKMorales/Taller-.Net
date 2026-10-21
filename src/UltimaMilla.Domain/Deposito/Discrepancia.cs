namespace UltimaMilla.Domain.Deposito;

public sealed class Discrepancia : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Discrepancia()
  {
    Envio = null!;
    Detalle = null!;
    DetalleDeclarado = null!;
    DetalleRecibido = null!;
  }

  public Envio Envio { get; private set; }
  public Guid EnvioId => Envio.Id;
  public Bulto? Bulto { get; private set; }
  public Guid? BultoId => Bulto?.Id;
  public TipoDiscrepancia Tipo { get; private set; }
  public string Detalle { get; private set; }
  public string DetalleDeclarado { get; private set; }
  public string DetalleRecibido { get; private set; }
  public DateTime FechaHora { get; private set; }

  public Discrepancia(Envio envio, Bulto? bulto, TipoDiscrepancia tipo, string detalle,
    string detalleDeclarado, string detalleRecibido, DateTime fechaHora)
  {
    Envio = Validacion.Requerido(envio, nameof(envio));
    if (bulto is not null && bulto.EnvioId != envio.Id)
      throw new InvalidOperationException("El bulto no pertenece al envio.");
    Bulto = bulto;
    Tipo = Validacion.Enumeracion(tipo, nameof(tipo));
    Detalle = Validacion.Texto(detalle, nameof(detalle));
    DetalleDeclarado = Validacion.Texto(detalleDeclarado, nameof(detalleDeclarado));
    DetalleRecibido = Validacion.Texto(detalleRecibido, nameof(detalleRecibido));
    Validacion.Fecha(fechaHora, nameof(fechaHora));
    if (fechaHora < envio.FechaAdmision) throw new ArgumentOutOfRangeException(nameof(fechaHora));
    FechaHora = fechaHora;
    envio.Registrar(this);
    bulto?.Registrar(this);
  }
}
