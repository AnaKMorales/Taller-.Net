namespace UltimaMilla.Domain.Despacho;

public sealed class Ruta : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Ruta()
  {
    Repartidor = null!;
    Vehiculo = null!;
  }

  private readonly List<Parada> _paradas = [];
  private readonly List<EscaneoBulto> _escaneos = [];
  public Repartidor Repartidor { get; private set; }
  public Guid RepartidorId => Repartidor.Id;
  public Vehiculo Vehiculo { get; private set; }
  public Guid VehiculoId => Vehiculo.Id;
  public Guid OperadorId => Vehiculo.OperadorId;
  public int MaxParadas { get; private set; }
  public DateTime? DespachadaEn { get; private set; }
  public Rendicion? Rendicion { get; private set; }
  public IReadOnlyCollection<Parada> Paradas => _paradas.AsReadOnly();
  public IReadOnlyCollection<EscaneoBulto> Escaneos => _escaneos.AsReadOnly();

  public Ruta(Repartidor repartidor, Vehiculo vehiculo, int maxParadas)
  {
    Repartidor = Validacion.Requerido(repartidor, nameof(repartidor));
    Vehiculo = Validacion.Requerido(vehiculo, nameof(vehiculo));
    Validacion.MismoOperador(repartidor.OperadorId, vehiculo.OperadorId);
    if (!repartidor.Activo) throw new InvalidOperationException("El repartidor esta inactivo.");
    MaxParadas = Validacion.Positivo(maxParadas, nameof(maxParadas));
    repartidor.Registrar(this);
    vehiculo.Registrar(this);
  }

  public Parada AgregarParada(Envio envio, int cod, DateTime horaEstimadaLlegada, Usuario responsable, DateTime ocurridoEn)
  {
    ArgumentNullException.ThrowIfNull(envio);
    if (DespachadaEn is not null || Rendicion is not null) throw new InvalidOperationException("La ruta ya no admite paradas.");
    Validacion.Positivo(cod, nameof(cod));
    if (_paradas.Any(p => p.Cod == cod)) throw new InvalidOperationException("El codigo de parada ya existe en esta ruta.");
    var activas = _paradas.Where(p => p.Activa).ToList();
    if (activas.Count >= MaxParadas) throw new InvalidOperationException("Se alcanzo el maximo de paradas.");
    envio.VerificarAsignacion(this, responsable, ocurridoEn, horaEstimadaLlegada);
    var bultos = envio.BultosParaAsignar();
    if (horaEstimadaLlegada < envio.VentanaDesde || horaEstimadaLlegada >= envio.VentanaHasta)
      throw new InvalidOperationException("La llegada no respeta la ventana del envio.");
    if (activas.Sum(p => p.PesoTotalKg) + bultos.Sum(b => b.Peso) > Vehiculo.CapacidadPesoKg ||
        activas.Sum(p => p.VolumenTotalM3) + bultos.Sum(b => b.VolumenM3) > Vehiculo.CapacidadVolumenM3)
      throw new InvalidOperationException("Se supera la capacidad del vehiculo.");
    var parada = new Parada(this, envio, cod, horaEstimadaLlegada, bultos);
    envio.Asignar(parada, responsable, ocurridoEn);
    _paradas.Add(parada);
    return parada;
  }

  public void Despachar(Usuario responsable, DateTime ocurridoEn)
  {
    if (DespachadaEn is not null || Rendicion is not null) throw new InvalidOperationException("La ruta ya fue despachada o rendida.");
    var activas = _paradas.Where(p => p.Activa).ToList();
    if (activas.Count == 0) throw new InvalidOperationException("No se puede despachar una ruta vacia.");
    foreach (var parada in activas) parada.Envio.VerificarInicio(responsable, ocurridoEn);
    foreach (var parada in activas) parada.Envio.IniciarEntrega(responsable, ocurridoEn);
    DespachadaEn = ocurridoEn;
  }

  public Rendicion Rendir(DateTime fecha)
  {
    Validacion.Fecha(fecha, nameof(fecha));
    if (Rendicion is not null) throw new InvalidOperationException("La ruta ya tiene rendicion.");
    if (DespachadaEn is null || fecha < DespachadaEn) throw new InvalidOperationException("La ruta debe estar despachada antes de rendirse.");
    if (_paradas.Any(p => p.Activa)) throw new InvalidOperationException("Quedan paradas pendientes.");
    Rendicion = new Rendicion(this, fecha);
    return Rendicion;
  }

  internal void Registrar(EscaneoBulto escaneo) => _escaneos.Add(escaneo);
}
