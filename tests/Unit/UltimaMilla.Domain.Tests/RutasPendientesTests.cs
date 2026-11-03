namespace UltimaMilla.Domain.Tests;

public sealed class RutasPendientesTests
{
  private static (Escenario E, Envio Envio, Ruta Primera) Parcial(
    AccionBultosPendientes politica = AccionBultosPendientes.NuevaVisitaSinRecargo)
  {
    var e = new Escenario(permiteEntregaParcial: true, accionPendientes: politica);
    var envio = e.CrearEnvio([new("A", 8, 80, 80, 80), new("B", 4, 50, 50, 50), new("C", 1, 10, 10, 10)]);
    var ruta = e.PonerEnTransito(envio);
    var b = envio.Bultos.ToArray();
    var foto = e.Foto();
    envio.RegistrarIntentoPorBulto(ruta.Paradas.Single().Id,
      [new(b[0].Id, ResultadoIntento.Entregado, null, [foto]), new(b[1].Id, ResultadoIntento.Entregado, null, [foto]),
       new(b[2].Id, ResultadoIntento.Fallido, e.Reintentable, [])], e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid());
    return (e, envio, ruta);
  }

  private static Ruta Reasignar(Escenario e, Envio envio, decimal peso = 1, decimal volumen = 0.001m)
  {
    envio.Reprogramar(e.CrearProgramacion(1), e.Administrador, e.Fecha.AddHours(3));
    var ruta = e.CrearRuta(peso: peso, volumen: volumen);
    ruta.AgregarParada(envio, 1, e.Fecha.AddDays(1).AddHours(2), e.Administrador, e.Fecha.AddHours(3));
    return ruta;
  }

  [Fact]
  public void SegundaRutaSoloLlevaPendienteYLaPrimeraConservaSuComposicion()
  {
    var (e, envio, primera) = Parcial();
    var segunda = Reasignar(e, envio);
    var parada = segunda.Paradas.Single();
    Assert.Equal("C", Assert.Single(parada.Bultos).Bulto.Codigo);
    Assert.Equal(1, parada.PesoTotalKg);
    Assert.Equal(0.001m, parada.VolumenTotalM3);
    Assert.Equal(13, envio.PesoTotalKg);
    Assert.Equal(3, primera.Paradas.Single().Bultos.Count);
    Assert.Equal(13, primera.Paradas.Single().PesoTotalKg);
    Assert.Equal(0.638m, primera.Paradas.Single().VolumenTotalM3);
    Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.Estado);
    Assert.Equal(EstadoBulto.AsignadoARuta, parada.Bultos.Single().Bulto.Estado);
    Assert.All(envio.Bultos.Where(b => b.Codigo != "C"), b => Assert.Equal(EstadoBulto.Entregado, b.Estado));
  }

  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public void CapacidadSumaSoloLaCargaDeLasParadasActivas(bool porPeso)
  {
    var (e, envio, _) = Parcial();
    var ruta = Reasignar(e, envio, porPeso ? 1.4m : 10, porPeso ? 1 : 0.0015m);
    var otro = e.CrearEnvio([new("OTRO", 0.5m, 10, 10, 10)], programacion: e.CrearProgramacion(1));
    e.RecibirEnDeposito(otro);
    Assert.Throws<InvalidOperationException>(() => ruta.AgregarParada(otro, 2, e.Fecha.AddDays(1).AddHours(2),
      e.Administrador, e.Fecha.AddHours(3)));
    Assert.Single(ruta.Paradas);
    Assert.Null(otro.Parada);
  }

  [Fact]
  public void CargaYEntregaFinalSoloAfectanAlPendiente()
  {
    var (e, envio, primera) = Parcial();
    var ruta = Reasignar(e, envio);
    var parada = ruta.Paradas.Single();
    Assert.Throws<InvalidOperationException>(() => EscaneoBulto.RegistrarCarga(ruta,
      envio.Bultos.Single(b => b.Codigo == "A"), e.Administrador, e.Fecha.AddDays(1)));
    EscaneoBulto.RegistrarCarga(ruta, parada.Bultos.Single().Bulto, e.Administrador, e.Fecha.AddDays(1));
    ruta.Despachar(e.Administrador, e.Fecha.AddDays(1));
    var foto = new PruebaEntrega(TipoPrueba.Foto, e.Fecha.AddDays(1).AddHours(2), new(-34.9m, -56.2m), new ArchivoRef("final.jpg"));
    var intento = envio.RegistrarIntentoPorBulto(parada.Id,
      [new(parada.Bultos.Single().Bulto.Id, ResultadoIntento.Entregado, null, [foto])],
      e.Repartidor, e.Fecha.AddDays(1).AddHours(2), Guid.NewGuid());
    Assert.Single(intento.Detalles);
    Assert.Equal(EstadoEnvio.Entregado, envio.Estado);
    Assert.All(envio.Bultos, b => Assert.Equal(EstadoBulto.Entregado, b.Estado));
    Assert.Equal(2, envio.Intentos.Count);
    Assert.False(primera.Paradas.Single().Activa);
    Assert.NotNull(ruta.Rendir(e.Fecha.AddDays(1).AddHours(3)));
  }

  [Fact]
  public void ResultadosAjenosALaSegundaVisitaYFirmaTradicionalSeRechazanSinCambios()
  {
    var (e, envio, _) = Parcial();
    var ruta = Reasignar(e, envio);
    ruta.Despachar(e.Administrador, e.Fecha.AddDays(1));
    Assert.Throws<InvalidOperationException>(() => envio.RegistrarIntento(ResultadoIntento.Entregado,
      null, [e.Foto()], e.Repartidor, e.Fecha.AddDays(1).AddHours(2), Guid.NewGuid()));
    Assert.Throws<InvalidOperationException>(() => envio.RegistrarIntentoPorBulto(envio.Parada!.Id,
      envio.Bultos.Select(b => new ResultadoBultoEntrega(b.Id, ResultadoIntento.Entregado, null, [e.Foto()])),
      e.Repartidor, e.Fecha.AddDays(1).AddHours(2), Guid.NewGuid()));
    Assert.Single(envio.Intentos);
    Assert.True(envio.Parada!.Activa);
    Assert.Equal(EstadoBulto.EnTransito, envio.Bultos.Single(b => b.Codigo == "C").Estado);
  }

  [Fact]
  public void FalloDeSegundaVisitaMantieneEntregadosYRespetaMaximoDeIntentos()
  {
    var (e, envio, _) = Parcial();
    var ruta = Reasignar(e, envio);
    ruta.Despachar(e.Administrador, e.Fecha.AddDays(1));
    envio.RegistrarIntentoPorBulto(envio.Parada!.Id,
      [new(envio.Bultos.Single(b => b.Codigo == "C").Id, ResultadoIntento.Fallido, e.Reintentable, [])],
      e.Repartidor, e.Fecha.AddDays(1).AddHours(2), Guid.NewGuid());
    Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.Estado);
    Assert.Equal(2, envio.Bultos.Count(b => b.Estado == EstadoBulto.Entregado));
    Assert.Throws<InvalidOperationException>(() => envio.Reprogramar(e.CrearProgramacion(2),
      e.Administrador, e.Fecha.AddDays(1).AddHours(3)));
  }

  [Theory]
  [InlineData(AccionBultosPendientes.DevolverAlComercio)]
  public void PoliticaDeDevolucionImpidePrepararNuevaVisita(AccionBultosPendientes politica)
  {
    var (e, envio, _) = Parcial(politica);
    var anterior = envio.Programacion;
    Assert.Throws<InvalidOperationException>(() => Reasignar(e, envio));
    Assert.Same(anterior, envio.Programacion);
    Assert.Null(envio.Parada);
    Assert.Equal(EstadoBulto.NoEntregado, envio.Bultos.Single(b => b.Codigo == "C").Estado);
  }

  [Fact]
  public void RetirarYAplazarConservanHistorialYNoDuplicanLaCargaActiva()
  {
    var (e, envio, _) = Parcial();
    var ruta = Reasignar(e, envio);
    envio.RetirarDeRuta(e.Administrador, e.Fecha.AddHours(4));
    Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.Estado);
    ruta.AgregarParada(envio, 2, e.Fecha.AddDays(1).AddHours(2), e.Administrador, e.Fecha.AddHours(4));
    Assert.Equal(2, ruta.Paradas.Count);
    Assert.Single(ruta.Paradas, p => p.Activa);
    envio.Aplazar(e.CrearProgramacion(2), e.Administrador, e.Fecha.AddHours(5));
    Assert.All(ruta.Paradas, p => Assert.False(p.Activa));
    Assert.Equal(EstadoBulto.Reprogramado, envio.Bultos.Single(b => b.Codigo == "C").Estado);
    var otra = e.CrearRuta(peso: 1, volumen: 0.001m);
    otra.AgregarParada(envio, 1, e.Fecha.AddDays(2).AddHours(2), e.Administrador, e.Fecha.AddHours(5));
    Assert.Single(otra.Paradas.Single().Bultos);
  }

  [Fact]
  public void VariasVisitasParcialesReducenLaSeleccionHastaCompletarElEnvio()
  {
    var e = new Escenario(maxIntentos: 3, permiteEntregaParcial: true);
    var envio = e.CrearEnvio(Enumerable.Range(1, 4).Select(n => new Bulto(n.ToString(), 1, 10, 10, 10)));
    var primera = e.PonerEnTransito(envio);
    Registrar(primera, 1, 0);
    envio.Reprogramar(e.CrearProgramacion(1), e.Administrador, e.Fecha.AddHours(3));
    var segunda = e.CrearRuta();
    segunda.AgregarParada(envio, 1, e.Fecha.AddDays(1).AddHours(2), e.Administrador, e.Fecha.AddHours(3));
    segunda.Despachar(e.Administrador, e.Fecha.AddDays(1));
    Registrar(segunda, 2, 1);
    Assert.Equal(3, envio.Bultos.Count(b => b.Estado == EstadoBulto.Entregado));
    envio.Reprogramar(e.CrearProgramacion(2), e.Administrador, e.Fecha.AddDays(1).AddHours(3));
    var tercera = e.CrearRuta(peso: 1, volumen: 0.001m);
    tercera.AgregarParada(envio, 1, e.Fecha.AddDays(2).AddHours(2), e.Administrador, e.Fecha.AddDays(1).AddHours(3));
    tercera.Despachar(e.Administrador, e.Fecha.AddDays(2));
    Registrar(tercera, 1, 2);
    Assert.Equal(EstadoEnvio.Entregado, envio.Estado);
    Assert.Equal(new[] { 4, 3, 1 }, envio.Intentos.Select(i => i.Detalles.Count));
    Assert.Equal(new[] { 4, 3, 1 }, new[] { primera, segunda, tercera }.Select(r => r.Paradas.Single().Bultos.Count));

    void Registrar(Ruta ruta, int entregados, int dia)
    {
      var fecha = e.Fecha.AddDays(dia).AddHours(2);
      var foto = new PruebaEntrega(TipoPrueba.Foto, fecha, new(-34.9m, -56.2m), new ArchivoRef($"foto-{dia}.jpg"));
      var parada = ruta.Paradas.Single();
      envio.RegistrarIntentoPorBulto(parada.Id, parada.Bultos.Select((b, i) => new ResultadoBultoEntrega(b.Bulto.Id,
        i < entregados ? ResultadoIntento.Entregado : ResultadoIntento.Fallido,
        i < entregados ? null : e.Reintentable, i < entregados ? [foto] : [])), e.Repartidor, fecha, Guid.NewGuid());
    }
  }
}
