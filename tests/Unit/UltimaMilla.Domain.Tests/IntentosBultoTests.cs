namespace UltimaMilla.Domain.Tests;

public sealed class IntentosBultoTests
{
  private static (Escenario E, Envio Envio, ResultadoBultoEntrega[] Datos, Ruta Ruta) Preparar(bool permite = true)
  {
    var e = new Escenario(permiteEntregaParcial: permite);
    var envio = e.CrearEnvio([new("A", 1, 10, 10, 10), new("B", 1, 10, 10, 10), new("C", 1, 10, 10, 10)]);
    var ruta = e.PonerEnTransito(envio);
    var b = envio.Bultos.ToArray();
    var foto = e.Foto();
    return (e, envio, [new(b[0].Id, ResultadoIntento.Entregado, null, [foto]),
      new(b[1].Id, ResultadoIntento.Entregado, null, [foto]), new(b[2].Id, ResultadoIntento.Fallido, e.Reintentable, [])], ruta);
  }

  [Fact]
  public void DosDeTresEntregadosCompartenPruebaYElPendienteConservaMotivo()
  {
    var (e, envio, datos, ruta) = Preparar();
    var intento = envio.RegistrarIntentoPorBulto(envio.Parada?.Id ?? envio.Intentos.Single().ParadaId, datos, e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid());
    Assert.Equal(ResultadoIntento.Parcial, intento.Resultado);
    Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.Estado);
    Assert.Equal(2, envio.Bultos.Count(b => b.Estado == EstadoBulto.Entregado));
    Assert.Single(envio.Bultos, b => b.Estado == EstadoBulto.NoEntregado);
    Assert.Equal(3, intento.Detalles.Count);
    var foto = Assert.Single(intento.Pruebas);
    Assert.Equal(2, intento.Detalles.SelectMany(d => d.Evidencias).Count(p => ReferenceEquals(p.Prueba, foto)));
    var pendiente = intento.Detalles.Single(d => d.Resultado == ResultadoIntento.Fallido);
    Assert.Same(e.Reintentable, pendiente.Motivo);
    Assert.Empty(pendiente.Evidencias);
    Assert.Equal(TipoEvento.EntregaParcial, envio.Eventos.Last().Tipo);
    Assert.Null(envio.Parada);
    Assert.False(ruta.Paradas.Single().Activa);
    Assert.NotNull(ruta.Rendir(e.Fecha.AddHours(3)));
  }

  [Fact]
  public void PermisoDesactivadoRechazaParcialSinCambiarNingunObjeto()
  {
    var (e, envio, datos, _) = Preparar(false);
    Assert.Throws<InvalidOperationException>(() => envio.RegistrarIntentoPorBulto(envio.Parada?.Id ?? envio.Intentos.Single().ParadaId, datos, e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid()));
    SinCambios(envio, datos);
  }

  [Theory]
  [InlineData("omitido")]
  [InlineData("ajeno")]
  [InlineData("duplicado")]
  [InlineData("motivo")]
  [InlineData("catalogo")]
  [InlineData("prueba")]
  [InlineData("posicion")]
  [InlineData("compartida")]
  public void SolicitudInvalidaEsAtomica(string caso)
  {
    var (e, envio, datos, _) = Preparar();
    datos = caso switch
    {
      "omitido" => datos[..2],
      "ajeno" => [datos[0] with { BultoId = Guid.NewGuid() }, datos[1], datos[2]],
      "duplicado" => [datos[0], datos[0], datos[2]],
      "motivo" => [datos[0], datos[1], datos[2] with { Motivo = null }],
      "catalogo" => [datos[0], datos[1], datos[2] with { Motivo = new Escenario().Reintentable }],
      "prueba" => [datos[0] with { Pruebas = [] }, datos[1], datos[2]],
      "posicion" => [datos[0] with { Pruebas = [new(TipoPrueba.Foto, e.Fecha.AddHours(2), null, new ArchivoRef("foto.jpg"))] }, datos[1], datos[2]],
      "compartida" => [datos[0], datos[1], datos[2] with { Pruebas = datos[0].Pruebas }],
      _ => throw new InvalidOperationException()
    };
    var error = Record.Exception(() => envio.RegistrarIntentoPorBulto(envio.Parada?.Id ?? envio.Intentos.Single().ParadaId, datos, e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid()));
    Assert.True(error is ArgumentException or InvalidOperationException);
    SinCambios(envio, datos);
  }

  [Fact]
  public void ReenvioConOtroOrdenYEvidenciasReconstruidasNoDuplica()
  {
    var (e, envio, datos, _) = Preparar();
    var id = Guid.NewGuid();
    var primero = envio.RegistrarIntentoPorBulto(envio.Parada?.Id ?? envio.Intentos.Single().ParadaId, datos, e.Repartidor, e.Fecha.AddHours(2), id);
    var repetidos = datos.Reverse().Select(d => d with { Pruebas = d.Pruebas.Select(_ => e.Foto()).ToArray() });
    Assert.Same(primero, envio.RegistrarIntentoPorBulto(envio.Parada?.Id ?? envio.Intentos.Single().ParadaId, repetidos, e.Repartidor, e.Fecha.AddHours(2), id));
    Assert.Single(envio.Intentos);
    Assert.Equal(5, envio.Eventos.Count);
  }

  [Theory]
  [InlineData("resultado")]
  [InlineData("fecha")]
  [InlineData("archivo")]
  [InlineData("responsable")]
  public void MismoIdentificadorConContenidoDistintoSeRechaza(string cambio)
  {
    var (e, envio, datos, _) = Preparar();
    var id = Guid.NewGuid();
    envio.RegistrarIntentoPorBulto(envio.Parada?.Id ?? envio.Intentos.Single().ParadaId, datos, e.Repartidor, e.Fecha.AddHours(2), id);
    var fecha = e.Fecha.AddHours(cambio == "fecha" ? 3 : 2);
    var responsable = cambio == "responsable" ? new Repartidor(e.Operador, "otro@example.com") : e.Repartidor;
    if (cambio == "resultado") datos[2] = datos[2] with { Resultado = ResultadoIntento.Entregado, Motivo = null, Pruebas = [e.Foto()] };
    if (cambio == "archivo") datos[0] = datos[0] with { Pruebas = [new(TipoPrueba.Foto, fecha, new(-34.9m, -56.2m), new ArchivoRef("otra.jpg"))] };
    Assert.Throws<InvalidOperationException>(() => envio.RegistrarIntentoPorBulto(envio.Parada?.Id ?? envio.Intentos.Single().ParadaId, datos, responsable, fecha, id));
    Assert.Single(envio.Intentos);
    Assert.Equal(5, envio.Eventos.Count);
  }

  [Fact]
  public void EntregaCompletaPorBultoNoRequierePermisoParcialYLaFirmaViejaNoAceptaParcial()
  {
    var (e, envio, datos, _) = Preparar(false);
    Assert.Throws<ArgumentException>(() => envio.RegistrarIntento(ResultadoIntento.Parcial, null, [], e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid()));
    datos[2] = datos[2] with { Resultado = ResultadoIntento.Entregado, Motivo = null, Pruebas = datos[0].Pruebas };
    envio.RegistrarIntentoPorBulto(envio.Parada?.Id ?? envio.Intentos.Single().ParadaId, datos, e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid());
    Assert.Equal(EstadoEnvio.Entregado, envio.Estado);
    Assert.All(envio.Bultos, b => Assert.Equal(EstadoBulto.Entregado, b.Estado));
  }

  private static void SinCambios(Envio envio, IEnumerable<ResultadoBultoEntrega> datos)
  {
    Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
    Assert.All(envio.Bultos, b => Assert.Equal(EstadoBulto.EnTransito, b.Estado));
    Assert.Empty(envio.Intentos);
    Assert.Equal(4, envio.Eventos.Count);
    Assert.True(envio.Parada!.Activa);
    Assert.All(datos.SelectMany(d => d.Pruebas), p => Assert.Null(p.Intento));
  }

  [Fact]
  public void OtraParadaNoPuedeAplicarUnIntentoAunqueElRepartidorCoincida()
  {
    var (e, envio, datos, _) = Preparar();
    Assert.Throws<InvalidOperationException>(() => envio.RegistrarIntentoPorBulto(Guid.NewGuid(), datos,
      e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid()));
    SinCambios(envio, datos);
  }

  [Fact]
  public void FalloCompletoConMotivosDistintosPermiteReintentoSiTodosSonReintentables()
  {
    var e = new Escenario();
    var otro = new MotivoNoEntrega(e.Reglas, "Otro", "Otro motivo", Responsable.Destinatario, false, true);
    var envio = e.CrearEnvio([new("A", 1, 10, 10, 10), new("B", 1, 10, 10, 10)]);
    var ruta = e.PonerEnTransito(envio);
    var bultos = envio.Bultos.ToArray();
    var intento = envio.RegistrarIntentoPorBulto(ruta.Paradas.Single().Id,
      [new(bultos[0].Id, ResultadoIntento.Fallido, e.Reintentable, []), new(bultos[1].Id, ResultadoIntento.Fallido, otro, [])],
      e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid());
    Assert.Null(intento.MotivoNoEntrega);
    envio.Reprogramar(e.CrearProgramacion(), e.Administrador, e.Fecha.AddHours(3));
    Assert.Equal(EstadoEnvio.Reprogramado, envio.Estado);
  }
}
