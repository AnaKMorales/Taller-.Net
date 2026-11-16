namespace UltimaMilla.Domain.Tests;

public sealed class PosicionVehiculoTests
{
  private static readonly GeoPunto Montevideo = new(-34.9011m, -56.1645m);

  private static (Escenario E, Ruta Ruta) Despachada()
  {
    var e = new Escenario();
    return (e, e.PonerEnTransito(e.CrearEnvio()));
  }

  [Fact]
  public void RegistraLaLecturaDeUnaRutaDespachadaDelMismoRepartidor()
  {
    var (e, ruta) = Despachada();
    var id = Guid.NewGuid();
    var lectura = new PosicionVehiculo(ruta, e.Repartidor, id, Montevideo, 12.5m, e.Fecha.AddMinutes(30), e.Fecha.AddHours(3));

    Assert.Same(ruta, lectura.Ruta);
    Assert.Equal(id, lectura.IdLectura);
    Assert.Equal(Montevideo, lectura.Punto);
    Assert.Equal(12.5m, lectura.PrecisionMetros);
    Assert.Equal(e.Fecha.AddMinutes(30), lectura.RegistradaEn);
    Assert.Equal(e.Fecha.AddHours(3), lectura.RecibidaEn);
    // No es parte del agregado Ruta: no altera sus paradas ni escaneos.
    Assert.Single(ruta.Paradas);
    Assert.Empty(ruta.Escaneos);
  }

  [Fact]
  public void RechazaUnaRutaSinDespachar()
  {
    var e = new Escenario();
    var ruta = e.CrearRuta();
    Assert.Throws<InvalidOperationException>(() =>
      new PosicionVehiculo(ruta, e.Repartidor, Guid.NewGuid(), Montevideo, null, e.Fecha, e.Fecha));
  }

  [Fact]
  public void RechazaLaRutaDeOtroRepartidor()
  {
    var (e, ruta) = Despachada();
    var otro = new Repartidor(e.Operador, "otro@example.com");
    Assert.Throws<InvalidOperationException>(() =>
      new PosicionVehiculo(ruta, otro, Guid.NewGuid(), Montevideo, null, e.Fecha.AddMinutes(1), e.Fecha.AddMinutes(1)));
  }

  [Theory]
  [InlineData(-10, 60)]        // antes del despacho, más allá de la tolerancia
  [InlineData(120, 60)]        // en el futuro respecto del servidor
  [InlineData(1, 60 * 25 + 1)] // más vieja que 24 h al recibirse
  public void RechazaHorasFueraDeRango(int registradaMinutos, int recibidaMinutos)
  {
    var (e, ruta) = Despachada();
    Assert.Throws<ArgumentOutOfRangeException>(() => new PosicionVehiculo(ruta, e.Repartidor, Guid.NewGuid(),
      Montevideo, null, e.Fecha.AddMinutes(registradaMinutos), e.Fecha.AddMinutes(recibidaMinutos)));
  }

  [Fact]
  public void ToleraUnRelojDelDispositivoLevementeAdelantado()
  {
    var (e, ruta) = Despachada();
    var lectura = new PosicionVehiculo(ruta, e.Repartidor, Guid.NewGuid(), Montevideo, null,
      e.Fecha.AddMinutes(14), e.Fecha.AddMinutes(10));
    Assert.Equal(e.Fecha.AddMinutes(14), lectura.RegistradaEn);
  }

  [Fact]
  public void RechazaIdVacioHorasNoUtcYPrecisionInvalida()
  {
    var (e, ruta) = Despachada();
    var hora = e.Fecha.AddMinutes(5);
    Assert.Throws<ArgumentException>(() => new PosicionVehiculo(ruta, e.Repartidor, Guid.Empty, Montevideo, null, hora, hora));
    var local = DateTime.SpecifyKind(hora, DateTimeKind.Local);
    Assert.Throws<ArgumentException>(() => new PosicionVehiculo(ruta, e.Repartidor, Guid.NewGuid(), Montevideo, null, local, hora));
    Assert.Throws<ArgumentOutOfRangeException>(() =>
      new PosicionVehiculo(ruta, e.Repartidor, Guid.NewGuid(), Montevideo, -1, hora, hora));
    Assert.Throws<ArgumentOutOfRangeException>(() =>
      new PosicionVehiculo(ruta, e.Repartidor, Guid.NewGuid(), Montevideo, 10_001, hora, hora));
  }
}
