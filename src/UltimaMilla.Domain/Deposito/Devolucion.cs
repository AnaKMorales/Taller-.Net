namespace UltimaMilla.Domain.Deposito;

public sealed class Devolucion : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Devolucion()
  {
    Envio = null!;
  }

  public Envio Envio { get; private set; }
  public Guid EnvioId => Envio.Id;
  public DateTime FechaInicio { get; private set; }
  public DateTime? FechaCierre { get; private set; }
  public EstadoDevolucion Estado { get; private set; } = EstadoDevolucion.EnProceso;
  public DateTime PlazoLimite { get; private set; }

  internal Devolucion(Envio envio, DateTime fechaInicio, DateTime plazoLimite)
  {
    Envio = envio;
    FechaInicio = fechaInicio;
    PlazoLimite = plazoLimite;
  }

  public void Completar(Usuario responsable, DateTime fechaCierre)
  {
    if (Estado == EstadoDevolucion.Completado) throw new InvalidOperationException("La devolucion ya esta completada.");
    if (fechaCierre < FechaInicio) throw new ArgumentOutOfRangeException(nameof(fechaCierre));
    if (Envio.Bultos.Any(b => !b.Escaneos.Any(e => e.Tipo == TipoEscaneo.Devolucion && e.DevolucionId == Id &&
        e.FechaHora >= FechaInicio && e.FechaHora <= fechaCierre)))
      throw new InvalidOperationException("Falta escanear un bulto de la devolucion.");
    Envio.ConfirmarDevolucion(this, responsable, fechaCierre);
    FechaCierre = fechaCierre;
    Estado = EstadoDevolucion.Completado;
  }
}
