namespace UltimaMilla.Domain.Deposito;

public sealed class EscaneoBulto : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private EscaneoBulto()
  {
    Bulto = null!;
    Responsable = null!;
  }

  public Ruta? Ruta { get; private set; }
  public Guid? RutaId => Ruta?.Id;
  public Devolucion? Devolucion { get; private set; }
  public Guid? DevolucionId => Devolucion?.Id;
  public Bulto Bulto { get; private set; }
  public Guid BultoId => Bulto.Id;
  public TipoEscaneo Tipo { get; private set; }
  public DateTime FechaHora { get; private set; }
  public Usuario Responsable { get; private set; }

  private EscaneoBulto(Bulto bulto, TipoEscaneo tipo, DateTime fechaHora, Usuario responsable,
    Ruta? ruta = null, Devolucion? devolucion = null)
  {
    Bulto = Validacion.Requerido(bulto, nameof(bulto));
    Responsable = Validacion.Requerido(responsable, nameof(responsable));
    Tipo = Validacion.Enumeracion(tipo, nameof(tipo));
    Validacion.Fecha(fechaHora, nameof(fechaHora));
    var envio = bulto.Envio ?? throw new InvalidOperationException("El bulto debe pertenecer a un envio.");
    Validacion.MismoOperador(envio.OperadorId, responsable.OperadorId);
    if (!responsable.Activo) throw new InvalidOperationException("El responsable esta inactivo.");
    if (fechaHora < envio.FechaAdmision) throw new ArgumentOutOfRangeException(nameof(fechaHora));

    switch (tipo)
    {
      case TipoEscaneo.Recepcion:
        if (envio.Estado != EstadoEnvio.Admitido)
          throw new InvalidOperationException("La recepcion se registra antes de confirmar el ingreso a deposito.");
        break;
      case TipoEscaneo.Carga:
        ArgumentNullException.ThrowIfNull(ruta);
        Validacion.MismoOperador(envio.OperadorId, ruta.OperadorId);
        if (ruta.DespachadaEn is not null || ruta.Rendicion is not null ||
            envio.Parada is not { Activa: true } || envio.Parada.RutaId != ruta.Id)
          throw new InvalidOperationException("La carga requiere la asignacion activa a una ruta sin despachar.");
        break;
      case TipoEscaneo.Devolucion:
        ArgumentNullException.ThrowIfNull(devolucion);
        if (devolucion.EnvioId != envio.Id || !ReferenceEquals(envio.Devolucion, devolucion) ||
            devolucion.Estado != EstadoDevolucion.EnProceso || envio.Estado != EstadoEnvio.EnDevolucion)
          throw new InvalidOperationException("El bulto debe pertenecer a la devolucion activa.");
        if (fechaHora < devolucion.FechaInicio) throw new ArgumentOutOfRangeException(nameof(fechaHora));
        if (ruta is not null)
        {
          Validacion.MismoOperador(envio.OperadorId, ruta.OperadorId);
          if (!ruta.Paradas.Any(p => p.EnvioId == envio.Id))
            throw new InvalidOperationException("La ruta de origen no contiene el envio.");
        }
        break;
    }

    Ruta = ruta;
    Devolucion = devolucion;
    FechaHora = fechaHora;
    ruta?.Registrar(this);
    bulto.Registrar(this);
  }

  public static EscaneoBulto RegistrarRecepcion(Bulto bulto, Usuario responsable, DateTime fechaHora) =>
    new(bulto, TipoEscaneo.Recepcion, fechaHora, responsable);

  public static EscaneoBulto RegistrarCarga(Ruta ruta, Bulto bulto, Usuario responsable, DateTime fechaHora) =>
    new(bulto, TipoEscaneo.Carga, fechaHora, responsable, ruta);

  public static EscaneoBulto RegistrarDevolucion(Devolucion devolucion, Bulto bulto, Usuario responsable,
    DateTime fechaHora, Ruta? rutaOrigen = null) =>
    new(bulto, TipoEscaneo.Devolucion, fechaHora, responsable, rutaOrigen, devolucion);
}
