namespace UltimaMilla.Domain.Configuracion;

public sealed class PlazoServicio : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private PlazoServicio()
  {
    Reglas = null!;
  }

  public ReglasOperativas Reglas { get; private set; }
  public Guid ReglasId => Reglas.Id;
  public ModalidadServicio ModalidadServicio { get; private set; }
  public int PlazoComprometidoHoras { get; private set; }
  public TimeOnly HoraCorte { get; private set; }

  public PlazoServicio(ReglasOperativas reglas, ModalidadServicio modalidadServicio, int plazoComprometidoHoras, TimeOnly horaCorte)
  {
    Reglas = Validacion.Requerido(reglas, nameof(reglas));
    ModalidadServicio = Validacion.Enumeracion(modalidadServicio, nameof(modalidadServicio));
    PlazoComprometidoHoras = Validacion.Positivo(plazoComprometidoHoras, nameof(plazoComprometidoHoras));
    HoraCorte = horaCorte;
    Reglas.Registrar(this);
  }
}
