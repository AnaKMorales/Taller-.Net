namespace UltimaMilla.Domain.Envios;

public sealed partial class Envio : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Envio()
  {
    CuentaComercial = null!;
    Destinatario = null!;
    Zona = null!;
    Reglas = null!;
    CuadroTarifario = null!;
    CodigoSeguimiento = null!;
    Programacion = null!;
  }

  private readonly List<Bulto> _bultos = [];
  private readonly List<EventoDeEnvio> _eventos = [];
  private readonly List<IntentoEntrega> _intentos = [];
  private readonly List<Discrepancia> _discrepancias = [];

  public CuentaComercial CuentaComercial { get; private set; }
  public Guid CuentaComercialId => CuentaComercial.Id;
  public Guid OperadorId => CuentaComercial.OperadorId;
  public Guid ComercioId => CuentaComercial.ComercioId;
  public Destinatario Destinatario { get; private set; }
  public Guid DestinatarioId => Destinatario.Id;
  public ZonaCobertura Zona { get; private set; }
  public Guid ZonaId => Zona.Id;
  public ReglasOperativas Reglas { get; private set; }
  public Guid ReglasId => Reglas.Id;
  public CuadroTarifario CuadroTarifario { get; private set; }
  public Guid CuadroTarifarioId => CuadroTarifario.Id;
  public string CodigoSeguimiento { get; private set; }
  public EstadoEnvio Estado { get; private set; } = EstadoEnvio.Admitido;
  public ModalidadServicio ModalidadServicio { get; private set; }
  public DateTime FechaAdmision { get; private set; }
  public DateTime FechaComprometida { get; private set; }
  public ProgramacionEntrega Programacion { get; private set; }
  public FranjaHoraria FranjaHoraria => Programacion.Franja;
  public Guid FranjaHorariaId => Programacion.FranjaId;
  public DateTime VentanaDesde => Programacion.VentanaDesde;
  public DateTime VentanaHasta => Programacion.VentanaHasta;
  public decimal TarifaCalculada { get; private set; }
  public Parada? Parada { get; private set; }
  public Devolucion? Devolucion { get; private set; }
  public decimal PesoTotalKg => _bultos.Sum(b => b.Peso);
  public decimal VolumenTotalM3 => _bultos.Sum(b => b.VolumenM3);
  public IReadOnlyCollection<Bulto> Bultos => _bultos.AsReadOnly();
  public IReadOnlyCollection<EventoDeEnvio> Eventos => _eventos.AsReadOnly();
  public IReadOnlyCollection<IntentoEntrega> Intentos => _intentos.AsReadOnly();
  public IReadOnlyCollection<Discrepancia> Discrepancias => _discrepancias.AsReadOnly();

  public Envio(CuentaComercial cuentaComercial, Destinatario destinatario, IEnumerable<Bulto> bultos,
    ModalidadServicio modalidadServicio, ZonaCobertura zona, ReglasOperativas reglas, CuadroTarifario cuadroTarifario,
    decimal tarifaCalculada, DateTime fechaAdmision, DateTime fechaComprometida,
    ProgramacionEntrega programacion, Usuario responsable)
  {
    CuentaComercial = Validacion.Requerido(cuentaComercial, nameof(cuentaComercial));
    Destinatario = Validacion.Requerido(destinatario, nameof(destinatario));
    Zona = Validacion.Requerido(zona, nameof(zona));
    Reglas = Validacion.Requerido(reglas, nameof(reglas));
    CuadroTarifario = Validacion.Requerido(cuadroTarifario, nameof(cuadroTarifario));
    Programacion = Validacion.Requerido(programacion, nameof(programacion));
    ModalidadServicio = Validacion.Enumeracion(modalidadServicio, nameof(modalidadServicio));
    TarifaCalculada = Validacion.NoNegativo(tarifaCalculada, nameof(tarifaCalculada));
    Validacion.MismoOperador(OperadorId, zona.OperadorId);
    Validacion.MismoOperador(OperadorId, reglas.OperadorId);
    Validacion.MismoOperador(OperadorId, cuadroTarifario.OperadorId);
    Validacion.Fecha(fechaAdmision, nameof(fechaAdmision));
    ValidarZonaProgramacion(programacion);
    if (fechaComprometida < fechaAdmision || programacion.VentanaHasta <= fechaAdmision ||
        fechaComprometida > programacion.VentanaHasta)
      throw new ArgumentException("Las fechas del envio son incompatibles.");
    if (reglas.VigenteDesde > fechaAdmision || cuadroTarifario.VigenteDesde > fechaAdmision)
      throw new InvalidOperationException("La configuracion todavia no esta vigente.");
    if (!reglas.PlazosServicio.Any(p => p.ModalidadServicio == modalidadServicio) ||
        !cuadroTarifario.Lineas.Any(l => l.ZonaId == zona.Id && l.ModalidadServicio == modalidadServicio))
      throw new InvalidOperationException("Falta configurar el plazo o la tarifa para esta zona y modalidad.");
    if (destinatario.Envio is not null) throw new InvalidOperationException("El destinatario ya pertenece a otro envio.");

    ArgumentNullException.ThrowIfNull(bultos);
    _bultos = bultos.ToList();
    if (_bultos.Count == 0 || _bultos.Any(b => b is null))
      throw new ArgumentException("El envio debe tener al menos un bulto.", nameof(bultos));
    if (_bultos.Any(b => b.Envio is not null) ||
        _bultos.Select(b => b.Id).Distinct().Count() != _bultos.Count ||
        _bultos.Select(b => b.Codigo).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _bultos.Count)
      throw new ArgumentException("Los bultos no pueden repetirse ni pertenecer a otro envio.", nameof(bultos));

    FechaAdmision = fechaAdmision;
    FechaComprometida = fechaComprometida;
    CodigoSeguimiento = Guid.NewGuid().ToString("N");
    ValidarResponsable(responsable, fechaAdmision);

    Estado = EstadoConjuntoEnvio.Calcular(_bultos.Select(b => b.Estado));
    foreach (var bulto in _bultos) bulto.Vincular(this);
    destinatario.Vincular(this);
    reglas.MarcarEnUso();
    cuadroTarifario.MarcarEnUso();
    cuentaComercial.Registrar(this);
    _eventos.Add(new EventoDeEnvio(this, TipoEvento.Admitir, OrigenEvento.Sistema, null,
      EstadoEnvio.Admitido, responsable, fechaAdmision, null, null, null, programacionNueva: Programacion));
  }

  public void RecibirEnDeposito(Usuario responsable, DateTime ocurridoEn)
  {
    VerificarEvento(TipoEvento.Validar, responsable, ocurridoEn);
    if (_bultos.Any(b => !b.Escaneos.Any(e => e.Tipo == TipoEscaneo.Recepcion && e.FechaHora <= ocurridoEn)))
      throw new InvalidOperationException("Falta registrar la recepcion de uno o mas bultos.");
    Aplicar(TipoEvento.Validar, responsable, ocurridoEn, OrigenEvento.Deposito);
  }

  internal void VerificarAsignacion(Ruta ruta, Usuario responsable, DateTime ocurridoEn, DateTime horaEstimadaLlegada)
  {
    Validacion.MismoOperador(OperadorId, ruta.OperadorId);
    if (Parada is not null) throw new InvalidOperationException("El envio ya tiene una asignacion activa.");
    var tipo = Estado == EstadoEnvio.Reprogramado ? TipoEvento.Reasignar : TipoEvento.Asignar;
    VerificarEvento(tipo, responsable, ocurridoEn);
    if (horaEstimadaLlegada < ocurridoEn)
      throw new InvalidOperationException("La llegada no puede ser anterior a la asignacion.");
    if (_intentos.Count > 0 && horaEstimadaLlegada < UltimoIntento!.OcurridoEn.AddHours(Reglas.PlazoEntreIntentosHoras))
      throw new InvalidOperationException("Todavia no se cumplio el plazo entre intentos.");
  }

  internal void Asignar(Parada parada, Usuario responsable, DateTime ocurridoEn)
  {
    var tipo = Estado == EstadoEnvio.Reprogramado ? TipoEvento.Reasignar : TipoEvento.Asignar;
    Parada = parada;
    Aplicar(tipo, responsable, ocurridoEn, OrigenEvento.Sistema);
  }

  public void RetirarDeRuta(Usuario responsable, DateTime ocurridoEn)
  {
    VerificarEvento(TipoEvento.Rechazar, responsable, ocurridoEn);
    LiberarParada();
    Aplicar(TipoEvento.Rechazar, responsable, ocurridoEn, OrigenEvento.Sistema);
  }

  public void Aplazar(ProgramacionEntrega nuevaProgramacion, Usuario responsable, DateTime ocurridoEn)
  {
    VerificarEvento(TipoEvento.Aplazar, responsable, ocurridoEn);
    ValidarNuevaProgramacion(nuevaProgramacion, ocurridoEn);
    var anterior = Programacion;
    LiberarParada();
    Programacion = nuevaProgramacion;
    Aplicar(TipoEvento.Aplazar, responsable, ocurridoEn, OrigenEvento.Sistema,
      programacionAnterior: anterior, programacionNueva: nuevaProgramacion);
  }

  internal void VerificarInicio(Usuario responsable, DateTime ocurridoEn) =>
    VerificarEvento(TipoEvento.IniciarEntrega, responsable, ocurridoEn);

  internal void IniciarEntrega(Usuario responsable, DateTime ocurridoEn) =>
    Aplicar(TipoEvento.IniciarEntrega, responsable, ocurridoEn, OrigenEvento.Sistema);

  public IntentoEntrega RegistrarIntento(ResultadoIntento resultado, MotivoNoEntrega? motivo,
    IEnumerable<PruebaEntrega> pruebas, Usuario responsable, DateTime ocurridoEn,
    Guid idOperacion, GeoPunto? posicion = null)
  {
    Validacion.Enumeracion(resultado, nameof(resultado));
    if (resultado == ResultadoIntento.Parcial)
      throw new ArgumentException("Usa el registro por bulto para una entrega parcial.", nameof(resultado));
    if (idOperacion == Guid.Empty) throw new ArgumentException("La operacion debe tener un identificador.", nameof(idOperacion));
    ValidarResponsable(responsable, ocurridoEn);
    var anterior = _intentos.SingleOrDefault(i => i.IdOperacion == idOperacion);
    if (anterior is not null)
    {
      if (anterior.Resultado != resultado || anterior.MotivoNoEntregaId != motivo?.Id)
        throw new InvalidOperationException("El identificador ya se uso para otro resultado.");
      return anterior;
    }

    var tipo = resultado == ResultadoIntento.Entregado ? TipoEvento.EntregaExitosa : TipoEvento.FalloEntrega;
    VerificarEvento(tipo, responsable, ocurridoEn);
    if (Parada is null) throw new InvalidOperationException("El envio no tiene parada activa.");
    if (responsable.Id != Parada.Ruta.RepartidorId)
      throw new InvalidOperationException("El intento debe registrarlo el repartidor asignado.");
    if (_intentos.Count >= Reglas.MaxIntentosEntrega) throw new InvalidOperationException("Se agotaron los intentos.");
    if (_intentos.Count > 0 && ocurridoEn < UltimoIntento!.OcurridoEn.AddHours(Reglas.PlazoEntreIntentosHoras))
      throw new InvalidOperationException("Todavia no se cumplio el plazo entre intentos.");

    ArgumentNullException.ThrowIfNull(pruebas);
    var lista = pruebas.ToList();
    if (lista.Any(p => p is null || p.Intento is not null) || lista.Select(p => p.Id).Distinct().Count() != lista.Count)
      throw new ArgumentException("Las pruebas no pueden repetirse ni pertenecer a otro intento.", nameof(pruebas));
    if (lista.Any(p => p.Posicion is null)) throw new InvalidOperationException("Las pruebas deben registrar la posicion del dispositivo.");
    if (resultado == ResultadoIntento.Entregado)
    {
      if (motivo is not null) throw new ArgumentException("Una entrega exitosa no lleva motivo de no entrega.", nameof(motivo));
      if (Reglas.TiposPrueba.Any(t => !lista.Any(p => p.TipoPrueba == t)))
        throw new InvalidOperationException("Falta una prueba exigida por el operador.");
    }
    else
    {
      ArgumentNullException.ThrowIfNull(motivo);
      if (motivo.ReglasId != ReglasId || !motivo.Activo)
        throw new InvalidOperationException("El motivo no pertenece al catalogo vigente del envio.");
      if (motivo.RequiereEvidencia && lista.Count == 0) throw new InvalidOperationException("El motivo exige evidencia.");
    }

    var parada = Parada;
    var intento = new IntentoEntrega(this, parada, _intentos.Count + 1, resultado, motivo, lista, idOperacion, ocurridoEn);
    foreach (var bulto in _bultos) intento.RegistrarDetalle(bulto, resultado, motivo, lista);
    _intentos.Add(intento);
    parada.Registrar(intento);
    Aplicar(tipo, responsable, ocurridoEn, OrigenEvento.AppRepartidor, posicion, null, idOperacion);
    LiberarParada();
    return intento;
  }

  public void Reprogramar(ProgramacionEntrega nuevaProgramacion, Usuario responsable, DateTime ocurridoEn)
  {
    VerificarEvento(TipoEvento.Reintentar, responsable, ocurridoEn);
    if (_intentos.Count == 0 || !UltimoIntento!.EsReintentable ||
        _intentos.Count >= Reglas.MaxIntentosEntrega)
      throw new InvalidOperationException("El envio no admite otro intento.");
    ValidarNuevaProgramacion(nuevaProgramacion, ocurridoEn);
    var anterior = Programacion;
    Programacion = nuevaProgramacion;
    Aplicar(TipoEvento.Reintentar, responsable, ocurridoEn, OrigenEvento.Sistema,
      programacionAnterior: anterior, programacionNueva: nuevaProgramacion);
  }

  public Devolucion IniciarDevolucion(Usuario responsable, DateTime ocurridoEn)
  {
    var tipo = Estado == EstadoEnvio.Reprogramado ? TipoEvento.Cancelar : TipoEvento.Devolver;
    VerificarEvento(tipo, responsable, ocurridoEn);
    if (Devolucion is not null) throw new InvalidOperationException("El envio ya tiene una devolucion.");
    if (Estado == EstadoEnvio.NoEntregado)
    {
      var noReintentable = UltimoIntento!.TieneMotivoNoReintentable;
      var agotados = _intentos.Count >= Reglas.MaxIntentosEntrega && Reglas.PoliticaDevolucion.DevolverAlAgotarIntentos;
      if (!noReintentable && !agotados) throw new InvalidOperationException("Corresponde reintentar antes de devolver.");
    }
    var devolucion = new Devolucion(this, ocurridoEn, ocurridoEn.AddDays(Reglas.PoliticaDevolucion.PlazoMaximoDevolucionDias));
    Devolucion = devolucion;
    Aplicar(tipo, responsable, ocurridoEn, OrigenEvento.Sistema);
    return devolucion;
  }

  internal void ConfirmarDevolucion(Devolucion devolucion, Usuario responsable, DateTime ocurridoEn)
  {
    VerificarEvento(TipoEvento.DevolucionExitosa, responsable, ocurridoEn);
    if (!ReferenceEquals(Devolucion, devolucion)) throw new InvalidOperationException("La devolucion no corresponde al envio.");
    Aplicar(TipoEvento.DevolucionExitosa, responsable, ocurridoEn, OrigenEvento.Deposito);
  }

  public void MarcarExtraviado(Usuario responsable, DateTime ocurridoEn, string detalle)
  {
    detalle = Validacion.Texto(detalle, nameof(detalle));
    VerificarEvento(TipoEvento.EventoAnomalo, responsable, ocurridoEn);
    LiberarParada();
    Aplicar(TipoEvento.EventoAnomalo, responsable, ocurridoEn, OrigenEvento.Sistema, null, detalle);
  }

  internal void Registrar(Discrepancia discrepancia) => _discrepancias.Add(discrepancia);

  // Una coleccion rehidratada no garantiza el orden de sus filas.
  private IntentoEntrega? UltimoIntento => _intentos.MaxBy(i => i.Numero);

  private void LiberarParada()
  {
    Parada?.Finalizar();
    Parada = null;
  }

  private void ValidarResponsable(Usuario responsable, DateTime ocurridoEn)
  {
    ArgumentNullException.ThrowIfNull(responsable);
    Validacion.MismoOperador(OperadorId, responsable.OperadorId);
    if (!responsable.Activo) throw new InvalidOperationException("El responsable esta inactivo.");
    if (responsable.Rol == RolUsuario.Comercio && !responsable.CuentasComerciales.Any(c => c.Id == CuentaComercialId))
      throw new InvalidOperationException("El usuario no pertenece a la cuenta comercial del envio.");
    Validacion.Fecha(ocurridoEn, nameof(ocurridoEn));
    if (ocurridoEn < FechaAdmision) throw new ArgumentException("El evento no puede ser anterior a la admision.", nameof(ocurridoEn));
  }

  private void VerificarEvento(TipoEvento tipo, Usuario responsable, DateTime ocurridoEn)
  {
    ValidarResponsable(responsable, ocurridoEn);
    _ = PrepararEstados(tipo);
  }

  private void ValidarZonaProgramacion(ProgramacionEntrega programacion)
  {
    if (programacion.Franja.ZonaId != ZonaId || !Zona.Franjas.Any(f => f.Id == programacion.FranjaId))
      throw new InvalidOperationException("La franja no pertenece a la zona del envio.");
  }

  private void ValidarNuevaProgramacion(ProgramacionEntrega programacion, DateTime ocurridoEn)
  {
    ArgumentNullException.ThrowIfNull(programacion);
    ValidarZonaProgramacion(programacion);
    if (programacion.VentanaHasta <= ocurridoEn)
      throw new InvalidOperationException("La nueva ventana ya finalizo.");
    if (_intentos.Count > 0 &&
        programacion.VentanaHasta <= UltimoIntento!.OcurridoEn.AddHours(Reglas.PlazoEntreIntentosHoras))
      throw new InvalidOperationException("La nueva ventana no permite respetar el plazo entre intentos.");
  }

  private void Aplicar(TipoEvento tipo, Usuario responsable, DateTime ocurridoEn, OrigenEvento origen,
    GeoPunto? posicion = null, string? detalle = null, Guid? idOperacion = null,
    ProgramacionEntrega? programacionAnterior = null, ProgramacionEntrega? programacionNueva = null)
  {
    var estados = PrepararEstados(tipo);
    var nuevo = EstadoConjuntoEnvio.Calcular(estados.Select(x => x.Estado));
    var evento = new EventoDeEnvio(this, tipo, origen, Estado, nuevo, responsable, ocurridoEn, posicion, detalle, idOperacion,
      programacionAnterior, programacionNueva);
    foreach (var cambio in estados) cambio.Bulto.ActualizarEstado(cambio.Estado);
    Estado = nuevo;
    _eventos.Add(evento);
  }

  private (Bulto Bulto, EstadoBulto Estado)[] PrepararEstados(TipoEvento tipo)
  {
    if (_bultos.Count == 0 || EstadoConjuntoEnvio.Calcular(_bultos.Select(b => b.Estado)) != Estado)
      throw new InvalidOperationException("Carga todos los bultos y sus estados coherentes antes de operar sobre el envio.");
    var esperado = TransicionesEnvio.Resolver(Estado, tipo);
    var cambios = _bultos.Select(b => (Bulto: b, Estado: TransicionesBulto.Resolver(b.Estado, tipo))).ToArray();
    if (EstadoConjuntoEnvio.Calcular(cambios.Select(x => x.Estado)) != esperado)
      throw new InvalidOperationException("La transicion de los bultos no coincide con la del envio.");
    return cambios;
  }
}
