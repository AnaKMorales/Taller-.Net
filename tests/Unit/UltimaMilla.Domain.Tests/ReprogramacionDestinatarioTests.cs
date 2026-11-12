namespace UltimaMilla.Domain.Tests;

public sealed class ReprogramacionDestinatarioTests
{
  [Fact]
  public void ElDestinatarioReprogramaTrasUnIntentoReintentableYElEventoNoTieneUsuario()
  {
    var e = new Escenario(maxIntentos: 2);
    var envio = e.CrearEnvio();
    e.PonerEnTransito(envio);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reintentable, [], e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid());
    Assert.Null(envio.ImpedimentoNuevaVisita());

    var nueva = e.CrearProgramacion(diasDespues: 1);
    envio.ReprogramarPorDestinatario(nueva, e.Fecha.AddHours(3));

    Assert.Equal(EstadoEnvio.Reprogramado, envio.Estado);
    Assert.Same(nueva, envio.Programacion);
    var evento = envio.Eventos.Last();
    Assert.Equal((TipoEvento.Reintentar, OrigenEvento.Destinatario), (evento.Tipo, evento.Origen));
    Assert.Null(evento.Responsable);
    Assert.Null(evento.ResponsableId);
    Assert.Same(nueva, evento.ProgramacionNueva);
    // Ya reprogramado, no puede volver a pedirlo: el envío no está esperando una nueva visita.
    Assert.NotNull(envio.ImpedimentoNuevaVisita());
    Assert.Throws<InvalidOperationException>(() => envio.ReprogramarPorDestinatario(e.CrearProgramacion(2), e.Fecha.AddHours(4)));
  }

  [Fact]
  public void NoSePuedeReprogramarConMotivoNoReintentableNiConIntentosAgotados()
  {
    var noReintentable = new Escenario();
    var a = noReintentable.CrearEnvio();
    noReintentable.PonerEnTransito(a);
    a.RegistrarIntento(ResultadoIntento.Fallido, noReintentable.NoReintentable, [], noReintentable.Repartidor,
      noReintentable.Fecha.AddHours(2), Guid.NewGuid());
    Assert.Contains("motivo", a.ImpedimentoNuevaVisita());
    var error = Assert.Throws<InvalidOperationException>(() =>
      a.ReprogramarPorDestinatario(noReintentable.CrearProgramacion(1), noReintentable.Fecha.AddHours(3)));
    Assert.StartsWith("No se puede reprogramar", error.Message);
    Assert.Equal(EstadoEnvio.NoEntregado, a.Estado);

    var agotado = new Escenario(maxIntentos: 1);
    var b = agotado.CrearEnvio();
    agotado.PonerEnTransito(b);
    b.RegistrarIntento(ResultadoIntento.Fallido, agotado.Reintentable, [], agotado.Repartidor, agotado.Fecha.AddHours(2), Guid.NewGuid());
    Assert.Contains("agotaron", b.ImpedimentoNuevaVisita());
    Assert.Throws<InvalidOperationException>(() => b.ReprogramarPorDestinatario(agotado.CrearProgramacion(1), agotado.Fecha.AddHours(3)));
  }

  [Fact]
  public void AntesDeUnIntentoFallidoNoHayNadaQueReprogramar()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    Assert.NotNull(envio.ImpedimentoNuevaVisita());
    Assert.Throws<InvalidOperationException>(() => envio.ReprogramarPorDestinatario(e.CrearProgramacion(1), e.Fecha.AddHours(1)));
    Assert.Equal(EstadoEnvio.Admitido, envio.Estado);
  }

  [Fact]
  public void LaNuevaVentanaDebeRespetarElPlazoEntreIntentos()
  {
    var e = new Escenario(maxIntentos: 3, plazoEntreIntentosHoras: 48);
    var envio = e.CrearEnvio();
    e.PonerEnTransito(envio);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reintentable, [], e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid());
    // Mismo día: no respeta las 48 h entre intentos.
    Assert.Throws<InvalidOperationException>(() => envio.ReprogramarPorDestinatario(e.CrearProgramacion(0), e.Fecha.AddHours(3)));
    envio.ReprogramarPorDestinatario(e.CrearProgramacion(3), e.Fecha.AddHours(3));
    Assert.Equal(EstadoEnvio.Reprogramado, envio.Estado);
  }

  [Fact]
  public void UnEventoSinUsuarioSoloPuedeVenirDelDestinatario()
  {
    // La invariante se prueba por el camino público: el operador no puede reprogramar sin usuario.
    var e = new Escenario();
    var envio = e.CrearEnvio();
    e.PonerEnTransito(envio);
    envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reintentable, [], e.Repartidor, e.Fecha.AddHours(2), Guid.NewGuid());
    Assert.Throws<ArgumentNullException>(() => envio.Reprogramar(e.CrearProgramacion(1), null!, e.Fecha.AddHours(3)));
    Assert.All(envio.Eventos, ev => Assert.NotNull(ev.Responsable));
  }
}
