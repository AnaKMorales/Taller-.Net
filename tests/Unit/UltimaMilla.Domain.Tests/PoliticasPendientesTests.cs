namespace UltimaMilla.Domain.Tests;

public sealed class PoliticasPendientesTests
{
  private static (Escenario E, Envio Envio, Ruta Primera) Parcial(AccionBultosPendientes accion,
    int maximo = 2, int entregados = 2)
  {
    var e = new Escenario(maxIntentos: maximo, permiteEntregaParcial: true, accionPendientes: accion,
      importeRecargoNuevaVisita: accion == AccionBultosPendientes.NuevaVisitaConRecargo ? 25 : null);
    var envio = e.CrearEnvio([new("A", 1, 10, 10, 10), new("B", 1, 10, 10, 10), new("C", 1, 10, 10, 10)]);
    var ruta = e.PonerEnTransito(envio);
    Registrar(e, envio, ruta.Paradas.Single(), entregados, 0);
    return (e, envio, ruta);
  }

  private static void Registrar(Escenario e, Envio envio, Parada parada, int entregados, int dia)
  {
    var fecha = e.Fecha.AddDays(dia).AddHours(2);
    var foto = new PruebaEntrega(TipoPrueba.Foto, fecha, new(-34.9m, -56.2m), new ArchivoRef($"foto-{dia}.jpg"));
    envio.RegistrarIntentoPorBulto(parada.Id, parada.Bultos.Select((b, i) => new ResultadoBultoEntrega(b.Bulto.Id,
      i < entregados ? ResultadoIntento.Entregado : ResultadoIntento.Fallido,
      i < entregados ? null : e.Reintentable, i < entregados ? [foto] : [])), e.Repartidor, fecha, Guid.NewGuid());
  }

  private static Ruta NuevaVisita(Escenario e, Envio envio, int dia)
  {
    envio.Reprogramar(e.CrearProgramacion(dia), e.Administrador, e.Fecha.AddDays(dia - 1).AddHours(3));
    var ruta = e.CrearRuta();
    ruta.AgregarParada(envio, 1, e.Fecha.AddDays(dia).AddHours(2), e.Administrador, e.Fecha.AddDays(dia - 1).AddHours(3));
    return ruta;
  }

  [Fact]
  public void PoliticaDevolverSeleccionaSoloPendientesYExigeSusEscaneos()
  {
    var (e, envio, ruta) = Parcial(AccionBultosPendientes.DevolverAlComercio, entregados: 1);
    var devolucion = envio.IniciarDevolucion(e.Administrador, e.Fecha.AddHours(3));
    Assert.Equal(2, devolucion.Bultos.Count);
    Assert.Throws<InvalidOperationException>(() => EscaneoBulto.RegistrarDevolucion(devolucion,
      envio.Bultos.Single(b => b.Estado == EstadoBulto.Entregado), e.Administrador, e.Fecha.AddHours(4), ruta));
    var pendientes = devolucion.Bultos.Select(b => b.Bulto).ToArray();
    EscaneoBulto.RegistrarDevolucion(devolucion, pendientes[0], e.Administrador, e.Fecha.AddHours(4), ruta);
    Assert.Throws<InvalidOperationException>(() => devolucion.Completar(e.Administrador, e.Fecha.AddHours(4)));
    EscaneoBulto.RegistrarDevolucion(devolucion, pendientes[1], e.Administrador, e.Fecha.AddHours(4), ruta);
    devolucion.Completar(e.Administrador, e.Fecha.AddHours(4));
    Assert.Equal(EstadoDevolucion.Completado, devolucion.Estado);
    Assert.Equal(2, envio.Bultos.Count(b => b.Estado == EstadoBulto.Devuelto));
    Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.Estado);
    Assert.Throws<InvalidOperationException>(() => NuevaVisita(e, envio, 1));
  }

  [Fact]
  public void ReintentoSinRecargoNoSeConvierteEnDevolucionAntesDeTiempo()
  {
    var (e, envio, _) = Parcial(AccionBultosPendientes.NuevaVisitaSinRecargo);
    Assert.Throws<InvalidOperationException>(() => envio.IniciarDevolucion(e.Administrador, e.Fecha.AddHours(3)));
    Assert.Null(envio.Devolucion);
    var ruta = NuevaVisita(e, envio, 1);
    ruta.Despachar(e.Administrador, e.Fecha.AddDays(1));
    Registrar(e, envio, ruta.Paradas.Single(), 0, 1);
    Assert.NotNull(envio.IniciarDevolucion(e.Administrador, e.Fecha.AddDays(1).AddHours(3)));
    Assert.Empty(envio.Recargos);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("0")]
  [InlineData("-1")]
  [InlineData("1.001")]
  public void ReglaConRecargoExigeImporteFijoPositivoConDosDecimales(string? texto)
  {
    var e = new Escenario();
    var importe = texto is null ? (decimal?)null : decimal.Parse(texto, System.Globalization.CultureInfo.InvariantCulture);
    Assert.Throws<ArgumentException>(() => new ReglasOperativas(e.Operador, 2, e.Fecha, 2, 0,
      [TipoPrueba.Foto], new(1, 5, true), true, AccionBultosPendientes.NuevaVisitaConRecargo, importe));
    Assert.Single(e.Operador.VersionesReglas);
  }

  [Fact]
  public void PoliticaSinRecargoNoAceptaUnImporteOculto()
  {
    var e = new Escenario();
    Assert.Throws<ArgumentException>(() => new ReglasOperativas(e.Operador, 2, e.Fecha, 2, 0,
      [TipoPrueba.Foto], new(1, 5, true), true, AccionBultosPendientes.NuevaVisitaSinRecargo, 25));
  }

  [Fact]
  public void ReasignarAntesDeSalirNoDevengaYDespacharRegistraUnaVez()
  {
    var (e, envio, _) = Parcial(AccionBultosPendientes.NuevaVisitaConRecargo);
    var ruta = NuevaVisita(e, envio, 1);
    var retirada = ruta.Paradas.Single();
    Assert.Equal(25, retirada.RecargoPrevisto);
    Assert.Empty(envio.Recargos);
    envio.RetirarDeRuta(e.Administrador, e.Fecha.AddHours(4));
    var activa = ruta.AgregarParada(envio, 2, e.Fecha.AddDays(1).AddHours(2), e.Administrador, e.Fecha.AddHours(4));
    ruta.Despachar(e.Administrador, e.Fecha.AddDays(1));
    var cargo = Assert.Single(envio.Recargos);
    Assert.Same(activa, cargo.Parada);
    Assert.Equal(25, cargo.Importe);
    Assert.Null(retirada.Recargo);
    Assert.Same(cargo, activa.Recargo);
    Assert.Equal(150, envio.TarifaCalculada);
    Assert.Throws<InvalidOperationException>(() => ruta.Despachar(e.Administrador, e.Fecha.AddDays(1)));
    Assert.Single(envio.Recargos);
  }

  [Fact]
  public void CadaVisitaDevengaUnFijoIndependienteDeCuantosBultosLleve()
  {
    var (e, envio, _) = Parcial(AccionBultosPendientes.NuevaVisitaConRecargo, maximo: 3, entregados: 1);
    var segunda = NuevaVisita(e, envio, 1);
    segunda.Despachar(e.Administrador, e.Fecha.AddDays(1));
    Registrar(e, envio, segunda.Paradas.Single(), 0, 1);
    var tercera = NuevaVisita(e, envio, 2);
    tercera.Despachar(e.Administrador, e.Fecha.AddDays(2));
    Registrar(e, envio, tercera.Paradas.Single(), 2, 2);
    Assert.Equal(2, envio.Recargos.Count);
    Assert.Equal(50, envio.Recargos.Sum(r => r.Importe));
    Assert.Equal(EstadoEnvio.Entregado, envio.Estado);
    Assert.Equal(150, envio.TarifaCalculada);
  }

  [Fact]
  public void VersionNuevaNoCambiaImporteDelEnvioAnterior()
  {
    var (e, envio, _) = Parcial(AccionBultosPendientes.NuevaVisitaConRecargo);
    _ = new ReglasOperativas(e.Operador, 2, e.Fecha, 3, 0, [TipoPrueba.Foto], new(1, 5, true), true,
      AccionBultosPendientes.NuevaVisitaConRecargo, 90);
    var ruta = NuevaVisita(e, envio, 1);
    ruta.Despachar(e.Administrador, e.Fecha.AddDays(1));
    Assert.Equal(25, Assert.Single(envio.Recargos).Importe);
    Assert.Same(e.Reglas, envio.Reglas);
  }
}
