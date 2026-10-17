namespace UltimaMilla.Domain.Tests;

public sealed class ConfiguracionTests
{
  [Fact]
  public void PlazoPorModalidadEsUnicoDentroDeLaVersion()
  {
    var e = new Escenario();
    Assert.Throws<InvalidOperationException>(() =>
      new PlazoServicio(e.Reglas, ModalidadServicio.Estandar, 48, new TimeOnly(14, 0)));
    Assert.Single(e.Reglas.PlazosServicio);
    _ = new PlazoServicio(e.Reglas, ModalidadServicio.Urgente, 12, new TimeOnly(12, 0));
    Assert.Equal(2, e.Reglas.PlazosServicio.Count);
  }

  [Fact]
  public void TarifasRechazanSuperposicionYAdmitenRangosContiguos()
  {
    var e = new Escenario();
    Assert.Throws<InvalidOperationException>(() =>
      new LineaTarifa(e.Cuadro, e.Zona, ModalidadServicio.Estandar, 50m, 150m, 10m, 200m));
    _ = new LineaTarifa(e.Cuadro, e.Zona, ModalidadServicio.Estandar, 100m, 200m, 10m, 250m);
    Assert.Equal(2, e.Cuadro.Lineas.Count);
  }

  [Fact]
  public void VersionesEnUsoQuedanInmutablesYElEnvioConservaSusReferencias()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    Assert.Throws<InvalidOperationException>(() =>
      new MotivoNoEntrega(e.Reglas, "Nuevo", "Nuevo motivo", Responsable.Operador, false, true));
    Assert.Throws<InvalidOperationException>(() =>
      new AjusteTarifario(Assert.Single(e.Cuadro.Lineas), TipoAjuste.Recargo, 5m));
    var nuevasReglas = new ReglasOperativas(e.Operador, 2, e.Fecha, 4, 24,
      [TipoPrueba.Firma], new PoliticaDevolucion(2, 10, true));
    Assert.Same(e.Reglas, envio.Reglas);
    Assert.NotSame(nuevasReglas, envio.Reglas);
    Assert.Equal(150m, envio.TarifaCalculada);
  }

  [Fact]
  public void ZonaExigeFranjaYNoPermiteCompartirla()
  {
    var op = new Operador("A");
    Assert.Throws<ArgumentException>(() => new ZonaCobertura(op, "Centro", "11000", []));
    var franja = new FranjaHoraria("Dia", new TimeOnly(8, 0), new TimeOnly(18, 0));
    var zona = new ZonaCobertura(op, "Centro", "11000", [franja]);
    Assert.Throws<ArgumentException>(() => new ZonaCobertura(op, "Otra", "12000", [franja]));
    Assert.Same(zona, franja.Zona);
    Assert.Single(op.Zonas);
  }

  [Theory]
  [InlineData(-91, 0)]
  [InlineData(91, 0)]
  [InlineData(0, -181)]
  [InlineData(0, 181)]
  public void CoordenadasFueraDeRangoSeRechazan(int latitud, int longitud) =>
    Assert.Throws<ArgumentOutOfRangeException>(() => new GeoPunto(latitud, longitud));
}
