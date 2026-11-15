namespace UltimaMilla.Domain.Tests;

public sealed class RelacionesTests
{
  [Fact]
  public void CuentaEditaDatosLocalesSinCambiarComercioCompartidoYBajaConservaHistorial()
  {
    var comercio = new Comercio("Común", "123", "c@example.com");
    var a = new CuentaComercial(new Operador("A"), comercio);
    var b = new CuentaComercial(new Operador("B"), comercio);
    var version = a.Version;
    a.ModificarDatos("Local", "456", "l@example.com");
    Assert.NotEqual(version, a.Version);
    Assert.Equal("Común", b.RazonSocial); Assert.Equal("Común", comercio.RazonSocial);
    version = a.Version;
    Assert.Throws<ArgumentException>(() => a.ModificarDatos("No guardar", " ", "x@example.com"));
    Assert.Equal("Local", a.RazonSocial); Assert.Equal(version, a.Version);
    a.DarBaja(); Assert.False(a.Activa); Assert.NotEqual(version, a.Version);
    version = a.Version; a.DarBaja(); Assert.Equal(version, a.Version);
    Assert.Throws<InvalidOperationException>(() => a.ModificarDatos("X", "123", "x@example.com"));
    Assert.True(b.Activa);
  }

  [Fact]
  public void UsuarioActualizaEmailSinCambiarRolYNoPuedeEditarTrasLaBaja()
  {
    var usuario = new Usuario(new Operador("Operador"), "inicial@example.com", RolUsuario.Comercio);
    usuario.ActualizarEmail(" nuevo@example.com ");
    Assert.Equal("nuevo@example.com", usuario.Email);
    Assert.Equal(RolUsuario.Comercio, usuario.Rol);
    Assert.Throws<ArgumentException>(() => usuario.ActualizarEmail(" "));
    usuario.Desactivar();
    Assert.Throws<InvalidOperationException>(() => usuario.ActualizarEmail("otro@example.com"));
    Assert.Equal("nuevo@example.com", usuario.Email);
  }

  [Fact]
  public void ComercioPuedeTrabajarConDosOperadoresConCuentasDistintas()
  {
    var comercio = new Comercio("Tienda", "099123456", "tienda@example.com");
    var a = new CuentaComercial(new Operador("A"), comercio);
    var b = new CuentaComercial(new Operador("B"), comercio);
    Assert.Equal(2, comercio.CuentasComerciales.Count);
    Assert.NotEqual(a.Id, b.Id);
    Assert.Same(comercio, a.Comercio);
    Assert.Single(a.Operador.CuentasComerciales);
    Assert.Throws<InvalidOperationException>(() => new CuentaComercial(a.Operador, comercio));
    Assert.Equal(2, comercio.CuentasComerciales.Count);
  }

  [Fact]
  public void AltaVinculaBultosDestinatarioDireccionCuentaYEvento()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    Assert.Same(envio, Assert.Single(envio.Bultos).Envio);
    Assert.Same(envio, envio.Destinatario.Envio);
    Assert.Same(envio.Destinatario, envio.Destinatario.Direccion.Destinatario);
    Assert.Same(envio, Assert.Single(e.Cuenta.Envios));
    var evento = Assert.Single(envio.Eventos);
    Assert.Same(envio, evento.Envio);
    Assert.Same(e.Administrador, evento.Responsable);
    Assert.Null(evento.EstadoAnterior);
    Assert.Equal(EstadoEnvio.Admitido, evento.EstadoNuevo);
    Assert.Equal(0.006m, envio.VolumenTotalM3);
  }

  [Fact]
  public void AltaRechazaEnvioSinBultosYNoModificaLaCuenta()
  {
    var e = new Escenario();
    Assert.Throws<ArgumentException>(() => e.CrearEnvio([]));
    Assert.Empty(e.Cuenta.Envios);
    Assert.False(e.Reglas.EnUso);
    Assert.False(e.Cuadro.EnUso);
  }

  [Fact]
  public void BultoYDestinatarioNoSeCompartenEntreEnvios()
  {
    var e = new Escenario();
    var original = e.CrearEnvio();
    var candidato = new Bulto("BUL-002", 1m, 10m, 10m, 10m);
    Assert.Throws<ArgumentException>(() => e.CrearEnvio(original.Bultos));
    Assert.Throws<InvalidOperationException>(() => e.CrearEnvio([candidato], original.Destinatario));
    Assert.Null(candidato.Envio);
    Assert.Single(e.Cuenta.Envios);
  }

  [Fact]
  public void DireccionNoSeComparteEntreDestinatarios()
  {
    var e = new Escenario();
    var destinatario = e.CrearDestinatario();
    Assert.Throws<InvalidOperationException>(() =>
      new Destinatario("Otra persona", "099111111", "otra@example.com", destinatario.Direccion));
    Assert.Same(destinatario, destinatario.Direccion.Destinatario);
  }

  [Fact]
  public void CodigosDeBultoDuplicadosSeRechazanSinVincularlos()
  {
    var e = new Escenario();
    var uno = new Bulto("BUL-001", 1m, 10m, 10m, 10m);
    var dos = new Bulto("bul-001", 1m, 10m, 10m, 10m);
    Assert.Throws<ArgumentException>(() => e.CrearEnvio([uno, dos]));
    Assert.Null(uno.Envio);
    Assert.Null(dos.Envio);
  }

  [Fact]
  public void UsuarioDeComercioSoloPuedeCrearParaSusCuentas()
  {
    var e = new Escenario();
    var otroComercio = new Comercio("Otro", "099222222", "otro@example.com");
    var otraCuenta = new CuentaComercial(e.Operador, otroComercio);
    var usuario = new Usuario(e.Operador, "usuario@example.com", RolUsuario.Comercio);
    usuario.AsignarCuentaComercial(e.Cuenta);
    Assert.Throws<InvalidOperationException>(() => e.CrearEnvio(cuenta: otraCuenta, responsable: usuario));
    Assert.Empty(otraCuenta.Envios);
    Assert.Same(e.Cuenta, e.CrearEnvio(responsable: usuario).CuentaComercial);
  }

  [Fact]
  public void RelacionesRechazanCrucesDeOperador()
  {
    var a = new Escenario();
    var b = new Escenario();
    Assert.Throws<InvalidOperationException>(() => new Ruta(a.Repartidor,
      new Vehiculo(b.Operador, "ABC123", 100m, 1m), 10));
    Assert.Throws<InvalidOperationException>(() =>
      new LineaTarifa(a.Cuadro, b.Zona, ModalidadServicio.Estandar, 100m, 200m, 1m, 100m));
    Assert.Throws<InvalidOperationException>(() => a.CrearEnvio(cuenta: b.Cuenta));
    var usuario = new Usuario(a.Operador, "cliente@example.com", RolUsuario.Comercio);
    Assert.Throws<InvalidOperationException>(() => usuario.AsignarCuentaComercial(b.Cuenta));
  }

  [Fact]
  public void ColeccionesNoPermitenAgregarRelacionesDesdeAfuera()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    var coleccion = Assert.IsAssignableFrom<ICollection<Bulto>>(envio.Bultos);
    Assert.True(coleccion.IsReadOnly);
    Assert.Throws<NotSupportedException>(() => coleccion.Clear());
    Assert.Single(envio.Bultos);
  }

  [Theory]
  [InlineData(0, 10, 10, 10)]
  [InlineData(1, 0, 10, 10)]
  [InlineData(1, 10, -1, 10)]
  [InlineData(1, 10, 10, 0)]
  public void BultoRechazaMedidasInvalidas(int peso, int alto, int ancho, int largo) =>
    Assert.Throws<ArgumentOutOfRangeException>(() => new Bulto("BUL", peso, alto, ancho, largo));
}
