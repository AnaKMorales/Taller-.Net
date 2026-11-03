namespace UltimaMilla.Domain.Tests;

internal sealed class Escenario
{
  public DateTime Fecha { get; } = new(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);
  public Operador Operador { get; } = new("Operador de prueba");
  public Comercio Comercio { get; } = new("Comercio", "099123456", "comercio@example.com");
  public CuentaComercial Cuenta { get; }
  public Usuario Administrador { get; }
  public Repartidor Repartidor { get; }
  public ZonaCobertura Zona { get; }
  public ReglasOperativas Reglas { get; }
  public CuadroTarifario Cuadro { get; }
  public MotivoNoEntrega Reintentable { get; }
  public MotivoNoEntrega NoReintentable { get; }

  public Escenario(int maxIntentos = 2, bool devolverAlAgotarIntentos = true, int plazoEntreIntentosHoras = 0,
    bool permiteEntregaParcial = false)
  {
    Cuenta = new CuentaComercial(Operador, Comercio);
    Administrador = new Usuario(Operador, "admin@example.com", RolUsuario.Administrador);
    Repartidor = new Repartidor(Operador, "repartidor@example.com");
    Zona = new ZonaCobertura(Operador, "Centro", "11000",
      [new FranjaHoraria("Dia", new TimeOnly(8, 0), new TimeOnly(18, 0))]);
    Reglas = new ReglasOperativas(Operador, 1, Fecha.AddDays(-1), maxIntentos, plazoEntreIntentosHoras,
      [TipoPrueba.Foto], new PoliticaDevolucion(1, 5, devolverAlAgotarIntentos), permiteEntregaParcial);
    _ = new PlazoServicio(Reglas, ModalidadServicio.Estandar, 24, new TimeOnly(16, 0));
    Reintentable = new MotivoNoEntrega(Reglas, "Ausente", "No habia nadie",
      Responsable.Destinatario, false, true);
    NoReintentable = new MotivoNoEntrega(Reglas, "Rechazo", "El receptor rechazo el envio",
      Responsable.Destinatario, false, false);
    Cuadro = new CuadroTarifario(Operador, 1, Fecha.AddDays(-1));
    _ = new LineaTarifa(Cuadro, Zona, ModalidadServicio.Estandar, 0m, 100m, 10m, 150m);
  }

  public Destinatario CrearDestinatario() =>
    new("Persona", "099000000", "persona@example.com", new Direccion("Calle", "0010 bis", "Montevideo", "01100"));

  public Envio CrearEnvio(IEnumerable<Bulto>? bultos = null, Destinatario? destinatario = null,
    CuentaComercial? cuenta = null, Usuario? responsable = null, ProgramacionEntrega? programacion = null) =>
    new(cuenta ?? Cuenta, destinatario ?? CrearDestinatario(), bultos ?? [new Bulto("BUL-001", 2m, 10m, 20m, 30m)],
      ModalidadServicio.Estandar, Zona, Reglas, Cuadro, 150m, Fecha, Fecha.AddHours(1),
      programacion ?? CrearProgramacion(), responsable ?? Administrador);

  public ProgramacionEntrega CrearProgramacion(int diasDespues = 0) =>
    ProgramacionEntrega.Crear(Zona.Franjas.Single(), DateOnly.FromDateTime(Fecha.AddDays(diasDespues)), TimeSpan.Zero);

  public void RecibirEnDeposito(Envio envio)
  {
    foreach (var bulto in envio.Bultos) EscaneoBulto.RegistrarRecepcion(bulto, Administrador, Fecha);
    envio.RecibirEnDeposito(Administrador, Fecha);
  }

  public Ruta CrearRuta(int maxParadas = 10, decimal peso = 100m, decimal volumen = 10m) =>
    new(Repartidor, new Vehiculo(Operador, Guid.NewGuid().ToString("N"), peso, volumen), maxParadas);

  public Ruta PonerEnTransito(Envio envio)
  {
    RecibirEnDeposito(envio);
    var ruta = CrearRuta();
    ruta.AgregarParada(envio, 1, Fecha.AddHours(2), Administrador, Fecha);
    ruta.Despachar(Administrador, Fecha);
    return ruta;
  }

  public PruebaEntrega Foto() =>
    new(TipoPrueba.Foto, Fecha.AddHours(2), new GeoPunto(-34.9m, -56.2m), new ArchivoRef("pruebas/foto.jpg"));
}
