namespace UltimaMilla.Domain.Tests;

public sealed class ProgramacionEntregaTests
{
  [Fact]
  public void FranjaLocalConservaFechaYSeRepresentaEnUtc()
  {
    var e = new Escenario();
    var fecha = new DateOnly(2026, 10, 15);
    var programacion = ProgramacionEntrega.Crear(e.Zona.Franjas.Single(), fecha, TimeSpan.FromHours(-3));
    Assert.Equal(fecha, programacion.FechaEntrega);
    Assert.Equal(8, programacion.InicioLocal.Hour);
    Assert.Equal(18, programacion.FinLocal.Hour);
    Assert.Equal(new DateTime(2026, 10, 15, 11, 0, 0, DateTimeKind.Utc), programacion.VentanaDesde);
    Assert.Equal(new DateTime(2026, 10, 15, 21, 0, 0, DateTimeKind.Utc), programacion.VentanaHasta);
  }

  [Theory]
  [InlineData(20, 21, 0)]
  [InlineData(9, 18, 0)]
  [InlineData(8, 17, 0)]
  [InlineData(8, 18, 1)]
  public void VentanaNoPuedeApartarseDeLaFechaYFranja(int desde, int hasta, int diasDesplazados)
  {
    var e = new Escenario();
    var fecha = DateOnly.FromDateTime(e.Fecha);
    var dia = fecha.AddDays(diasDesplazados);
    Assert.Throws<ArgumentException>(() => new ProgramacionEntrega(e.Zona.Franjas.Single(), fecha,
      new DateTimeOffset(dia.ToDateTime(new TimeOnly(desde, 0)), TimeSpan.Zero),
      new DateTimeOffset(dia.ToDateTime(new TimeOnly(hasta, 0)), TimeSpan.Zero)));
  }

  [Fact]
  public void FranjaDeOtraZonaSeRechazaAunqueSeaDelMismoOperador()
  {
    var e = new Escenario();
    var otraZona = new ZonaCobertura(e.Operador, "Otra", "12000",
      [new FranjaHoraria("Dia", new TimeOnly(8, 0), new TimeOnly(18, 0))]);
    var ajena = ProgramacionEntrega.Crear(otraZona.Franjas.Single(), DateOnly.FromDateTime(e.Fecha), TimeSpan.Zero);
    var destinatario = e.CrearDestinatario();
    var bulto = new Bulto("A", 1m, 10m, 10m, 10m);
    Assert.Throws<InvalidOperationException>(() => e.CrearEnvio([bulto], destinatario, programacion: ajena));
    Assert.Null(destinatario.Envio);
    Assert.Null(bulto.Envio);
    Assert.Empty(e.Cuenta.Envios);
  }

  [Fact]
  public void ReprogramarAlDiaSiguienteConservaCompromisoYAmbasVentanasEnElEvento()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    var original = envio.Programacion;
    var compromiso = envio.FechaComprometida;
    e.PonerEnTransito(envio);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reintentable, [], e.Repartidor,
      e.Fecha.AddHours(2), Guid.NewGuid());
    var nueva = e.CrearProgramacion(1);
    envio.Reprogramar(nueva, e.Administrador, e.Fecha.AddHours(3));

    var evento = envio.Eventos.Last();
    Assert.Same(original, evento.ProgramacionAnterior);
    Assert.Same(nueva, evento.ProgramacionNueva);
    Assert.Same(original, envio.Eventos.First().ProgramacionNueva);
    Assert.Same(nueva.Franja, envio.FranjaHoraria);
    Assert.Equal(compromiso, envio.FechaComprometida);
    Assert.Equal(e.Fecha.AddDays(1), envio.VentanaDesde);

    var ruta = e.CrearRuta();
    ruta.AgregarParada(envio, 1, e.Fecha.AddDays(1).AddHours(2), e.Administrador, e.Fecha.AddHours(3));
    ruta.Despachar(e.Administrador, e.Fecha.AddDays(1));
    var foto = new PruebaEntrega(TipoPrueba.Foto, e.Fecha.AddDays(1).AddHours(2),
      new GeoPunto(-34m, -56m), new ArchivoRef("pruebas/nueva-foto.jpg"));
    envio.RegistrarIntento(ResultadoIntento.Entregado, null, [foto], e.Repartidor,
      e.Fecha.AddDays(1).AddHours(2), Guid.NewGuid());
    Assert.Equal(EstadoEnvio.Entregado, envio.Estado);
    Assert.Equal(2, envio.Intentos.Count);
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void ProgramacionInvalidaNoCambiaEstadoNiHistorial(bool zonaAjena)
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.PonerEnTransito(envio);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reintentable, [], e.Repartidor,
      e.Fecha.AddHours(2), Guid.NewGuid());
    var original = envio.Programacion;
    var eventos = envio.Eventos.Count;
    var otraZona = new ZonaCobertura(e.Operador, "Otra", "12000",
      [new FranjaHoraria("Dia", new TimeOnly(8, 0), new TimeOnly(18, 0))]);
    var candidata = zonaAjena
      ? ProgramacionEntrega.Crear(otraZona.Franjas.Single(), DateOnly.FromDateTime(e.Fecha.AddDays(1)), TimeSpan.Zero)
      : e.CrearProgramacion();
    Assert.Throws<InvalidOperationException>(() =>
      envio.Reprogramar(candidata, e.Administrador, e.Fecha.AddDays(1)));
    Assert.Same(original, envio.Programacion);
    Assert.Equal(EstadoEnvio.NoEntregado, envio.Estado);
    Assert.Equal(eventos, envio.Eventos.Count);
  }

  [Fact]
  public void AplazarTambienActualizaLaVentanaYLiberaLaParada()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.RecibirEnDeposito(envio);
    var primera = e.CrearRuta().AgregarParada(envio, 1, e.Fecha.AddHours(2), e.Administrador, e.Fecha);
    var anterior = envio.Programacion;
    var nueva = e.CrearProgramacion(1);
    envio.Aplazar(nueva, e.Administrador, e.Fecha);
    Assert.False(primera.Activa);
    Assert.Null(envio.Parada);
    Assert.Equal(EstadoEnvio.Reprogramado, envio.Estado);
    Assert.Same(anterior, envio.Eventos.Last().ProgramacionAnterior);
    Assert.Same(nueva, envio.Eventos.Last().ProgramacionNueva);
    e.CrearRuta().AgregarParada(envio, 1, nueva.VentanaDesde.AddHours(1), e.Administrador, e.Fecha);
    Assert.Equal(EstadoEnvio.AsignadoARuta, envio.Estado);
  }

  [Fact]
  public void NuevaVentanaDebePermitirRespetarElPlazoEntreIntentos()
  {
    var e = new Escenario(plazoEntreIntentosHoras: 24);
    var envio = e.CrearEnvio();
    e.PonerEnTransito(envio);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reintentable, [], e.Repartidor,
      e.Fecha.AddHours(2), Guid.NewGuid());
    Assert.Throws<InvalidOperationException>(() => envio.Reprogramar(e.CrearProgramacion(), e.Administrador, e.Fecha.AddHours(3)));
    envio.Reprogramar(e.CrearProgramacion(1), e.Administrador, e.Fecha.AddHours(3));
    var ruta = e.CrearRuta();
    Assert.Throws<InvalidOperationException>(() =>
      ruta.AgregarParada(envio, 1, e.Fecha.AddDays(1).AddHours(1), e.Administrador, e.Fecha.AddHours(3)));
    Assert.Empty(ruta.Paradas);
    // Se permite planificar ahora un intento que ocurrira una vez cumplidas las 24 horas.
    ruta.AgregarParada(envio, 1, e.Fecha.AddDays(1).AddHours(2), e.Administrador, e.Fecha.AddHours(3));
    ruta.Despachar(e.Administrador, e.Fecha.AddDays(1));
    Assert.Throws<InvalidOperationException>(() => envio.RegistrarIntento(ResultadoIntento.Fallido,
      e.Reintentable, [], e.Repartidor, e.Fecha.AddDays(1).AddHours(1), Guid.NewGuid()));
    Assert.Single(envio.Intentos);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reintentable, [], e.Repartidor,
      e.Fecha.AddDays(1).AddHours(2), Guid.NewGuid());
    Assert.Equal(2, envio.Intentos.Count);
  }

  [Fact]
  public void LaLlegadaNoPuedeProgramarseEnElFinalExclusivoDeLaFranja()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.RecibirEnDeposito(envio);
    var ruta = e.CrearRuta();
    Assert.Throws<InvalidOperationException>(() =>
      ruta.AgregarParada(envio, 1, envio.VentanaHasta, e.Administrador, e.Fecha));
    Assert.Empty(ruta.Paradas);
  }
}
