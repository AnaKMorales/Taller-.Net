namespace UltimaMilla.Domain.Tests;

public sealed class DespachoYEstadosTests
{
  [Fact]
  public void EntregaCompletaRegistraEstadosPruebasYLiberaAsignacion()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    var ruta = e.PonerEnTransito(envio);
    var parada = Assert.Single(ruta.Paradas);
    var prueba = e.Foto();
    var intento = envio.RegistrarIntento(ResultadoIntento.Entregado, null, [prueba],
      e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid(), prueba.Posicion);
    Assert.Equal(EstadoEnvio.Entregado, envio.Estado);
    Assert.Null(envio.Parada);
    Assert.False(parada.Activa);
    Assert.Same(intento, prueba.Intento);
    Assert.Same(parada, intento.Parada);
    Assert.Same(intento, Assert.Single(parada.Intentos));
    Assert.Equal(new[] { TipoEvento.Admitir, TipoEvento.Validar, TipoEvento.Asignar,
      TipoEvento.IniciarEntrega, TipoEvento.EntregaExitosa }, envio.Eventos.Select(ev => ev.Tipo));
    Assert.Same(ruta, ruta.Rendir(e.Fecha.AddHours(3)).Ruta);
    Assert.Throws<InvalidOperationException>(() => ruta.Rendir(e.Fecha.AddHours(4)));
    Assert.Throws<InvalidOperationException>(() => envio.MarcarExtraviado(e.Administrador, e.Fecha.AddHours(4), "No permitido"));
    Assert.Equal(5, envio.Eventos.Count);
  }

  [Fact]
  public void UnEnvioNoPuedeAsignarseADosRutas()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.RecibirEnDeposito(envio);
    var primera = e.CrearRuta();
    primera.AgregarParada(envio, 1, e.Fecha.AddHours(2), e.Administrador, e.Fecha);
    var segunda = e.CrearRuta();
    Assert.Throws<InvalidOperationException>(() =>
      segunda.AgregarParada(envio, 1, e.Fecha.AddHours(2), e.Administrador, e.Fecha));
    Assert.Empty(segunda.Paradas);
    Assert.Same(primera, envio.Parada!.Ruta);
  }

  [Theory]
  [InlineData(1, 100, 10)]
  [InlineData(10, 3, 10)]
  [InlineData(10, 100, 0.01)]
  public void RutaRechazaExcesoDeParadasPesoOVolumen(int maxParadas, int peso, double volumen)
  {
    var e = new Escenario();
    var primero = e.CrearEnvio();
    var segundo = e.CrearEnvio();
    e.RecibirEnDeposito(primero);
    e.RecibirEnDeposito(segundo);
    var ruta = e.CrearRuta(maxParadas, peso, (decimal)volumen);
    ruta.AgregarParada(primero, 1, e.Fecha.AddHours(2), e.Administrador, e.Fecha);
    Assert.Throws<InvalidOperationException>(() =>
      ruta.AgregarParada(segundo, 2, e.Fecha.AddHours(2), e.Administrador, e.Fecha));
    Assert.Single(ruta.Paradas);
    Assert.Null(segundo.Parada);
    Assert.Equal(EstadoEnvio.EnDeposito, segundo.Estado);
  }

  [Fact]
  public void RutaRechazaVentanaIncompatibleSinCambiarEnvio()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.RecibirEnDeposito(envio);
    var ruta = e.CrearRuta();
    Assert.Throws<InvalidOperationException>(() =>
      ruta.AgregarParada(envio, 1, e.Fecha.AddHours(11), e.Administrador, e.Fecha));
    Assert.Empty(ruta.Paradas);
    Assert.Equal(2, envio.Eventos.Count);
    Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
  }

  [Fact]
  public void NoSePuedeAsignarUnEnvioAdmitido()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    var ruta = e.CrearRuta();
    Assert.Throws<InvalidOperationException>(() =>
      ruta.AgregarParada(envio, 1, e.Fecha.AddHours(2), e.Administrador, e.Fecha));
    Assert.Equal(EstadoEnvio.Admitido, envio.Estado);
    Assert.Single(envio.Eventos);
  }

  [Fact]
  public void RetirarYAplazarConservanParadasHistoricasYPermitenReasignar()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.RecibirEnDeposito(envio);
    var ruta = e.CrearRuta();
    var primera = ruta.AgregarParada(envio, 1, e.Fecha.AddHours(2), e.Administrador, e.Fecha);
    envio.RetirarDeRuta(e.Administrador, e.Fecha);
    Assert.False(primera.Activa);
    Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
    ruta.AgregarParada(envio, 2, e.Fecha.AddHours(2), e.Administrador, e.Fecha);
    envio.Aplazar(e.CrearProgramacion(), e.Administrador, e.Fecha);
    Assert.Equal(EstadoEnvio.Reprogramado, envio.Estado);
    var nueva = ruta.AgregarParada(envio, 3, e.Fecha.AddHours(2), e.Administrador, e.Fecha);
    Assert.True(nueva.Activa);
    Assert.Equal(TipoEvento.Reasignar, envio.Eventos.Last().Tipo);
    Assert.Equal(3, ruta.Paradas.Count);
  }

  [Fact]
  public void IntentoSinPruebaExigidaNoCambiaEstadoNiVinculaPruebas()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.PonerEnTransito(envio);
    var nombre = new PruebaEntrega(TipoPrueba.Nombre, e.Fecha.AddHours(2),
      new GeoPunto(-34m, -56m), valorTexto: "Persona");
    Assert.Throws<InvalidOperationException>(() => envio.RegistrarIntento(ResultadoIntento.Entregado,
      null, [nombre], e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid()));
    Assert.Null(nombre.Intento);
    Assert.Empty(envio.Intentos);
    Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
    Assert.Equal(4, envio.Eventos.Count);
  }

  [Fact]
  public void ReenvioOfflineEsIdempotenteYColisionDeResultadoSeRechaza()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.PonerEnTransito(envio);
    var operacion = Guid.NewGuid();
    var prueba = e.Foto();
    var original = envio.RegistrarIntento(ResultadoIntento.Entregado, null, [prueba],
      e.Repartidor, e.Fecha.AddHours(2), operacion);
    var repetido = envio.RegistrarIntento(ResultadoIntento.Entregado, null, [prueba],
      e.Repartidor, e.Fecha.AddHours(2), operacion);
    Assert.Same(original, repetido);
    Assert.Single(envio.Intentos);
    Assert.Equal(5, envio.Eventos.Count);
    Assert.Throws<InvalidOperationException>(() => envio.RegistrarIntento(ResultadoIntento.Fallido,
      e.Reintentable, [], e.Repartidor, e.Fecha.AddHours(2), operacion));
  }

  [Fact]
  public void MotivoReintentablePermiteNuevaRutaHastaAgotarIntentos()
  {
    var e = new Escenario(maxIntentos: 2);
    var envio = e.CrearEnvio();
    e.PonerEnTransito(envio);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reintentable, [], e.Repartidor,
      e.Fecha.AddHours(2), Guid.NewGuid());
    Assert.Throws<InvalidOperationException>(() => envio.IniciarDevolucion(e.Administrador, e.Fecha.AddHours(3)));
    envio.Reprogramar(e.CrearProgramacion(), e.Administrador, e.Fecha.AddHours(3));
    var ruta = e.CrearRuta();
    ruta.AgregarParada(envio, 1, e.Fecha.AddHours(4), e.Administrador, e.Fecha.AddHours(3));
    ruta.Despachar(e.Administrador, e.Fecha.AddHours(3));
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reintentable, [], e.Repartidor,
      e.Fecha.AddHours(4), Guid.NewGuid());
    Assert.Throws<InvalidOperationException>(() => envio.Reprogramar(e.CrearProgramacion(), e.Administrador, e.Fecha.AddHours(5)));
    Assert.NotNull(envio.IniciarDevolucion(e.Administrador, e.Fecha.AddHours(5)));
  }

  [Fact]
  public void DevolucionExigeEscaneoDeTodosLosBultosAntesDeCerrar()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio([new Bulto("A", 1m, 10m, 10m, 10m), new Bulto("B", 1m, 10m, 10m, 10m)]);
    var ruta = e.PonerEnTransito(envio);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.NoReintentable, [], e.Repartidor,
      e.Fecha.AddHours(2), Guid.NewGuid());
    Assert.Throws<InvalidOperationException>(() => envio.Reprogramar(e.CrearProgramacion(), e.Administrador, e.Fecha.AddHours(2)));
    var devolucion = envio.IniciarDevolucion(e.Administrador, e.Fecha.AddHours(3));
    Assert.Throws<InvalidOperationException>(() => devolucion.Completar(e.Administrador, e.Fecha.AddHours(4)));
    var bultos = envio.Bultos.ToArray();
    _ = EscaneoBulto.RegistrarDevolucion(devolucion, bultos[0], e.Administrador, e.Fecha.AddHours(4), ruta);
    Assert.Throws<InvalidOperationException>(() => devolucion.Completar(e.Administrador, e.Fecha.AddHours(4)));
    _ = EscaneoBulto.RegistrarDevolucion(devolucion, bultos[1], e.Administrador, e.Fecha.AddHours(4), ruta);
    devolucion.Completar(e.Administrador, e.Fecha.AddHours(4));
    Assert.Equal(EstadoEnvio.Devuelto, envio.Estado);
    Assert.Equal(EstadoDevolucion.Completado, devolucion.Estado);
    Assert.Equal(e.Fecha.AddHours(4), devolucion.FechaCierre);
    Assert.Throws<InvalidOperationException>(() => envio.MarcarExtraviado(e.Administrador, e.Fecha.AddHours(5), "No"));
  }

  [Fact]
  public void ExtravioSoloSeAdmiteDesdeEstadosOperativos()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    Assert.Throws<InvalidOperationException>(() => envio.MarcarExtraviado(e.Administrador, e.Fecha, "Perdido"));
    e.RecibirEnDeposito(envio);
    envio.MarcarExtraviado(e.Administrador, e.Fecha.AddHours(1), "No se encontro el bulto");
    Assert.Equal(EstadoEnvio.Extraviado, envio.Estado);
    Assert.Throws<InvalidOperationException>(() => envio.RecibirEnDeposito(e.Administrador, e.Fecha.AddHours(2)));
    Assert.Equal(3, envio.Eventos.Count);
  }
}
