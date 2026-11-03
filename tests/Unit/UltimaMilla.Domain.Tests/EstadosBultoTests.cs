namespace UltimaMilla.Domain.Tests;

public sealed class EstadosBultoTests
{
  [Fact]
  public void RecorridoCompletoActualizaTodosLosBultosYElResumen()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio([new("A", 1, 10, 10, 10), new("B", 1, 10, 10, 10)]);
    Verificar(EstadoBulto.Admitido, EstadoEnvio.Admitido);
    e.RecibirEnDeposito(envio);
    Verificar(EstadoBulto.EnDeposito, EstadoEnvio.EnDeposito);
    var ruta = e.CrearRuta();
    ruta.AgregarParada(envio, 1, e.Fecha.AddHours(2), e.Administrador, e.Fecha);
    Verificar(EstadoBulto.AsignadoARuta, EstadoEnvio.AsignadoARuta);
    ruta.Despachar(e.Administrador, e.Fecha);
    Verificar(EstadoBulto.EnTransito, EstadoEnvio.EnTransito);
    var operacion = Guid.NewGuid();
    envio.RegistrarIntento(ResultadoIntento.Entregado, null, [e.Foto()], e.Repartidor, e.Fecha.AddHours(2), operacion);
    Verificar(EstadoBulto.Entregado, EstadoEnvio.Entregado);
    envio.RegistrarIntento(ResultadoIntento.Entregado, null, [], e.Repartidor, e.Fecha.AddHours(2), operacion);
    Verificar(EstadoBulto.Entregado, EstadoEnvio.Entregado);
    Assert.Equal(5, envio.Eventos.Count);

    void Verificar(EstadoBulto bulto, EstadoEnvio conjunto)
    {
      Assert.All(envio.Bultos, b => Assert.Equal(bulto, b.Estado));
      Assert.Equal(conjunto, envio.Estado);
      Assert.Equal(conjunto, EstadoConjuntoEnvio.Calcular(envio.Bultos.Select(b => b.Estado)));
      Assert.Equal(conjunto, envio.Eventos.Last().EstadoNuevo);
    }
  }

  [Fact]
  public void ReintentoAplazamientoYDevolucionMantienenEstadosCoherentes()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    var ruta = e.PonerEnTransito(envio);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reintentable, [], e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid());
    Assert.Equal(EstadoBulto.NoEntregado, envio.Bultos.Single().Estado);
    envio.Reprogramar(e.CrearProgramacion(), e.Administrador, e.Fecha.AddHours(3));
    Assert.Equal(EstadoBulto.Reprogramado, envio.Bultos.Single().Estado);
    ruta = e.CrearRuta();
    ruta.AgregarParada(envio, 1, e.Fecha.AddHours(4), e.Administrador, e.Fecha.AddHours(3));
    envio.RetirarDeRuta(e.Administrador, e.Fecha.AddHours(3));
    Assert.Equal(EstadoBulto.EnDeposito, envio.Bultos.Single().Estado);
    ruta.AgregarParada(envio, 2, e.Fecha.AddHours(4), e.Administrador, e.Fecha.AddHours(3));
    envio.Aplazar(e.CrearProgramacion(), e.Administrador, e.Fecha.AddHours(3));
    var devolucion = envio.IniciarDevolucion(e.Administrador, e.Fecha.AddHours(4));
    Assert.Equal(EstadoBulto.EnDevolucion, envio.Bultos.Single().Estado);
    EscaneoBulto.RegistrarDevolucion(devolucion, envio.Bultos.Single(), e.Administrador, e.Fecha.AddHours(5));
    devolucion.Completar(e.Administrador, e.Fecha.AddHours(5));
    Assert.Equal(EstadoBulto.Devuelto, envio.Bultos.Single().Estado);
    Assert.Equal(EstadoEnvio.Devuelto, envio.Estado);
  }

  [Fact]
  public void OperacionInvalidaNoCambiaBultosNiHistorial()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.PonerEnTransito(envio);
    Assert.Throws<InvalidOperationException>(() => envio.RegistrarIntento(ResultadoIntento.Entregado,
      null, [], e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid()));
    Assert.Equal(EstadoBulto.EnTransito, envio.Bultos.Single().Estado);
    Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
    Assert.Empty(envio.Intentos);
    Assert.Equal(4, envio.Eventos.Count);
  }

  [Fact]
  public void ExtravioEsTerminalYNoHayMutadorPublicoDeEstado()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.RecibirEnDeposito(envio);
    envio.MarcarExtraviado(e.Administrador, e.Fecha, "No encontrado");
    Assert.Equal(EstadoBulto.Extraviado, envio.Bultos.Single().Estado);
    foreach (var terminal in new[] { EstadoBulto.Entregado, EstadoBulto.Devuelto, EstadoBulto.Extraviado })
      foreach (var evento in Enum.GetValues<TipoEvento>())
        Assert.Throws<InvalidOperationException>(() => TransicionesBulto.Resolver(terminal, evento));
    Assert.False(typeof(Bulto).GetProperty(nameof(Bulto.Estado))!.SetMethod!.IsPublic);
  }

  [Theory]
  [InlineData(EstadoBulto.NoEntregado)]
  [InlineData(EstadoBulto.Reprogramado)]
  [InlineData(EstadoBulto.EnTransito)]
  [InlineData(EstadoBulto.EnDevolucion)]
  [InlineData(EstadoBulto.Devuelto)]
  [InlineData(EstadoBulto.Extraviado)]
  public void AlgunosEntregadosDerivanEnEntregaParcialSinDependerDelOrden(EstadoBulto otro)
  {
    Assert.Equal(EstadoEnvio.EntregadoParcialmente,
      EstadoConjuntoEnvio.Calcular([EstadoBulto.Entregado, otro, EstadoBulto.Entregado]));
    Assert.Equal(EstadoEnvio.EntregadoParcialmente,
      EstadoConjuntoEnvio.Calcular([otro, EstadoBulto.Entregado]));
  }

  [Fact]
  public void ResumenDistingueEntregaCompletaDevueltosYPendientes()
  {
    Assert.Equal(EstadoEnvio.Entregado, EstadoConjuntoEnvio.Calcular([EstadoBulto.Entregado, EstadoBulto.Entregado]));
    Assert.Equal(EstadoEnvio.Devuelto, EstadoConjuntoEnvio.Calcular([EstadoBulto.Devuelto, EstadoBulto.Devuelto]));
    Assert.Equal(EstadoEnvio.EnTransito, EstadoConjuntoEnvio.Calcular([EstadoBulto.Devuelto, EstadoBulto.EnTransito]));
    Assert.Equal(EstadoEnvio.Extraviado, EstadoConjuntoEnvio.Calcular([EstadoBulto.NoEntregado, EstadoBulto.Extraviado]));
    Assert.Equal(EstadoEnvio.EnDevolucion, EstadoConjuntoEnvio.Calcular([EstadoBulto.NoEntregado, EstadoBulto.EnDevolucion]));
  }

  [Fact]
  public void ResumenRechazaColeccionVaciaYEstadosDesconocidos()
  {
    Assert.Throws<ArgumentException>(() => EstadoConjuntoEnvio.Calcular([]));
    Assert.Throws<ArgumentOutOfRangeException>(() => EstadoConjuntoEnvio.Calcular([(EstadoBulto)999]));
    Assert.Throws<InvalidOperationException>(() => TransicionesBulto.Resolver((EstadoBulto)999, TipoEvento.Validar));
  }
}
