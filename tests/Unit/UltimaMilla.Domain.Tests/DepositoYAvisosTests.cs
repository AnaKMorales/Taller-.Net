namespace UltimaMilla.Domain.Tests;

public sealed class DepositoYAvisosTests
{
  [Fact]
  public void DiscrepanciaPuedeSerDelEnvioODeUnoDeSusBultos()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    var bulto = Assert.Single(envio.Bultos);
    var general = new Discrepancia(envio, null, TipoDiscrepancia.Otro, "Observacion",
      "Declarado", "Recibido", e.Fecha);
    var especifica = new Discrepancia(envio, bulto, TipoDiscrepancia.PesoNoCoincide, "Peso diferente",
      "2 kg", "3 kg", e.Fecha);
    Assert.Equal(2, envio.Discrepancias.Count);
    Assert.Null(general.Bulto);
    Assert.Same(especifica, Assert.Single(bulto.Discrepancias));
    var ajeno = Assert.Single(e.CrearEnvio().Bultos);
    Assert.Throws<InvalidOperationException>(() => new Discrepancia(envio, ajeno,
      TipoDiscrepancia.Otro, "No", "A", "B", e.Fecha));
  }

  [Fact]
  public void AvisosAislanComerciosDelMismoOperadorYEvitanDuplicados()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    var suscripcion = new SuscripcionAviso(e.Cuenta, [TipoEvento.Admitir], "secreto",
      new Uri("https://comercio.example.com/webhooks"));
    var entrega = new EntregaAviso(suscripcion, Assert.Single(envio.Eventos));
    Assert.Same(entrega, Assert.Single(suscripcion.Entregas));
    Assert.Throws<InvalidOperationException>(() => new EntregaAviso(suscripcion, Assert.Single(envio.Eventos)));
    var otraCuenta = new CuentaComercial(e.Operador, new Comercio("Otro", "099111111", "otro@example.com"));
    var otroEnvio = e.CrearEnvio(cuenta: otraCuenta);
    Assert.Throws<InvalidOperationException>(() => new EntregaAviso(suscripcion, Assert.Single(otroEnvio.Eventos)));
    entrega.RegistrarIntento(false, e.Fecha.AddMinutes(1));
    entrega.RegistrarIntento(true, e.Fecha.AddMinutes(2));
    Assert.Equal(2, entrega.Intentos);
    Assert.True(entrega.Exitoso);
    Assert.Throws<InvalidOperationException>(() => entrega.RegistrarIntento(true, e.Fecha.AddMinutes(3)));
  }

  [Fact]
  public void SuscripcionInactivaOEventoNoSeleccionadoNoGeneranEntrega()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    var evento = Assert.Single(envio.Eventos);
    var suscripcion = new SuscripcionAviso(e.Cuenta, [TipoEvento.EntregaExitosa], "secreto", new Uri("https://example.com"));
    Assert.Throws<InvalidOperationException>(() => new EntregaAviso(suscripcion, evento));
    var otra = new SuscripcionAviso(e.Cuenta, [TipoEvento.Admitir], "secreto", new Uri("https://example.com"));
    otra.Desactivar();
    Assert.Throws<InvalidOperationException>(() => new EntregaAviso(otra, evento));
    Assert.Empty(otra.Entregas);
  }

  [Fact]
  public void NotificacionQuedaVinculadaAlDestinatarioYSeMarcaUnaVez()
  {
    var e = new Escenario();
    var envio = e.CrearEnvio();
    var notificacion = new Notificacion(envio.Destinatario, CanalNotificacion.Email, "Su envio fue admitido.");
    Assert.Same(notificacion, Assert.Single(envio.Destinatario.Notificaciones));
    Assert.Null(notificacion.EnviadaEn);
    notificacion.MarcarEnviada(e.Fecha.AddMinutes(1));
    Assert.Equal(e.Fecha.AddMinutes(1), notificacion.EnviadaEn);
    Assert.Throws<InvalidOperationException>(() => notificacion.MarcarEnviada(e.Fecha.AddMinutes(2)));
  }

  [Fact]
  public void DomainNoDependeDeProyectosNiFrameworksExternos()
  {
    var referencias = typeof(Envio).Assembly.GetReferencedAssemblies();
    Assert.DoesNotContain(referencias, r => r.Name!.StartsWith("Microsoft.EntityFrameworkCore") ||
      r.Name.StartsWith("Microsoft.AspNetCore") || r.Name.StartsWith("UltimaMilla.Application") ||
      r.Name.StartsWith("UltimaMilla.Infrastructure"));
    Assert.All(typeof(EventoDeEnvio).GetProperties(), p => Assert.False(p.SetMethod?.IsPublic ?? false));
    Assert.False(typeof(Envio).GetProperty(nameof(Envio.Estado))!.SetMethod!.IsPublic);
  }
}
