namespace UltimaMilla.Domain.Tests;

public sealed class EscaneosTests
{
  [Fact]
  public void RecepcionPermiteEscanearTodosLosBultosSinCrearRuta()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio([new Bulto("A", 1m, 10m, 10m, 10m), new Bulto("B", 1m, 10m, 10m, 10m)]);
    var bultos = envio.Bultos.ToArray();
    var primero = EscaneoBulto.RegistrarRecepcion(bultos[0], e.Administrador, e.Fecha);
    Assert.Null(primero.Ruta);
    Assert.Null(primero.Devolucion);
    Assert.Throws<InvalidOperationException>(() => envio.RecibirEnDeposito(e.Administrador, e.Fecha));
    Assert.Equal(EstadoEnvio.Admitido, envio.Estado);
    Assert.Single(envio.Eventos);
    EscaneoBulto.RegistrarRecepcion(bultos[1], e.Administrador, e.Fecha);
    envio.RecibirEnDeposito(e.Administrador, e.Fecha);
    Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
    Assert.Empty(e.Repartidor.Rutas);
    Assert.All(bultos, b => Assert.Single(b.Escaneos));
  }

  [Fact]
  public void EscaneoPosteriorALaConfirmacionNoCuentaComoRecepcion()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    EscaneoBulto.RegistrarRecepcion(envio.Bultos.Single(), e.Administrador, e.Fecha.AddMinutes(1));
    Assert.Throws<InvalidOperationException>(() => envio.RecibirEnDeposito(e.Administrador, e.Fecha));
    envio.RecibirEnDeposito(e.Administrador, e.Fecha.AddMinutes(1));
    Assert.Throws<InvalidOperationException>(() =>
      EscaneoBulto.RegistrarRecepcion(envio.Bultos.Single(), e.Administrador, e.Fecha.AddMinutes(2)));
  }

  [Fact]
  public void ResponsableDeOtroOperadorNoPuedeEscanearElBulto()
  {
    var e = new Escenario();
    var ajeno = new Escenario();
    var envio = e.CrearEnvio();
    var bulto = envio.Bultos.Single();
    Assert.Throws<InvalidOperationException>(() => EscaneoBulto.RegistrarRecepcion(bulto, ajeno.Administrador, e.Fecha));
    Assert.Empty(bulto.Escaneos);
  }

  [Fact]
  public void CargaExigeAsignacionActivaARutaSinDespachar()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    var ruta = e.CrearRuta();
    var bulto = envio.Bultos.Single();
    Assert.Throws<ArgumentNullException>(() => EscaneoBulto.RegistrarCarga(null!, bulto, e.Administrador, e.Fecha));
    Assert.Throws<InvalidOperationException>(() => EscaneoBulto.RegistrarCarga(ruta, bulto, e.Administrador, e.Fecha));
    e.RecibirEnDeposito(envio);
    ruta.AgregarParada(envio, 1, e.Fecha.AddHours(2), e.Administrador, e.Fecha);
    var otra = e.CrearRuta();
    Assert.Throws<InvalidOperationException>(() => EscaneoBulto.RegistrarCarga(otra, bulto, e.Administrador, e.Fecha));
    var carga = EscaneoBulto.RegistrarCarga(ruta, bulto, e.Repartidor, e.Fecha);
    Assert.Same(carga, ruta.Escaneos.Single());
    Assert.Same(ruta, carga.Ruta);
    ruta.Despachar(e.Administrador, e.Fecha);
    Assert.Throws<InvalidOperationException>(() => EscaneoBulto.RegistrarCarga(ruta, bulto, e.Repartidor, e.Fecha.AddMinutes(1)));
    Assert.Empty(otra.Escaneos);
  }

  [Fact]
  public void RetirarElEnvioDeLaRutaImpideEscanearSuCarga()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.RecibirEnDeposito(envio);
    var ruta = e.CrearRuta();
    ruta.AgregarParada(envio, 1, e.Fecha.AddHours(2), e.Administrador, e.Fecha);
    envio.RetirarDeRuta(e.Administrador, e.Fecha);
    Assert.Throws<InvalidOperationException>(() =>
      EscaneoBulto.RegistrarCarga(ruta, envio.Bultos.Single(), e.Administrador, e.Fecha));
    Assert.Empty(ruta.Escaneos);
  }

  [Fact]
  public void DevolucionSeEscaneaEnDepositoSinRutaYQuedaLigadaAlProceso()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.PonerEnTransito(envio);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.NoReintentable, [], e.Repartidor,
      e.Fecha.AddHours(2), Guid.NewGuid());
    var devolucion = envio.IniciarDevolucion(e.Administrador, e.Fecha.AddHours(3));
    var bulto = envio.Bultos.Single();
    Assert.Throws<ArgumentOutOfRangeException>(() =>
      EscaneoBulto.RegistrarDevolucion(devolucion, bulto, e.Administrador, e.Fecha.AddHours(2)));
    var escaneo = EscaneoBulto.RegistrarDevolucion(devolucion, bulto, e.Administrador, e.Fecha.AddHours(4));
    Assert.Null(escaneo.Ruta);
    Assert.Same(devolucion, escaneo.Devolucion);
    devolucion.Completar(e.Administrador, e.Fecha.AddHours(4));
    Assert.Equal(EstadoEnvio.Devuelto, envio.Estado);
    Assert.Throws<InvalidOperationException>(() =>
      EscaneoBulto.RegistrarDevolucion(devolucion, bulto, e.Administrador, e.Fecha.AddHours(5)));
  }

  [Fact]
  public void DevolucionRechazaBultoAjenoYRutaDeOrigenIncorrecta()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.PonerEnTransito(envio);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.NoReintentable, [], e.Repartidor,
      e.Fecha.AddHours(2), Guid.NewGuid());
    var devolucion = envio.IniciarDevolucion(e.Administrador, e.Fecha.AddHours(3));
    var ajeno = e.CrearEnvio().Bultos.Single();
    Assert.Throws<InvalidOperationException>(() =>
      EscaneoBulto.RegistrarDevolucion(devolucion, ajeno, e.Administrador, e.Fecha.AddHours(4)));
    var otraRuta = e.CrearRuta();
    Assert.Throws<InvalidOperationException>(() => EscaneoBulto.RegistrarDevolucion(devolucion,
      envio.Bultos.Single(), e.Administrador, e.Fecha.AddHours(4), otraRuta));
    Assert.Empty(ajeno.Escaneos);
    Assert.Empty(otraRuta.Escaneos);
  }
}
