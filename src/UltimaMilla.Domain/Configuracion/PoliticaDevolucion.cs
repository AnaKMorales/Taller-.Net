namespace UltimaMilla.Domain.Configuracion;

public sealed class PoliticaDevolucion : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private PoliticaDevolucion()
  {
  }

  public int DiasEnDeposito { get; private set; }
  public int PlazoMaximoDevolucionDias { get; private set; }
  public bool DevolverAlAgotarIntentos { get; private set; }
  public ReglasOperativas? Reglas { get; private set; }

  public PoliticaDevolucion(int diasEnDeposito, int plazoMaximoDevolucionDias, bool devolverAlAgotarIntentos)
  {
    if (diasEnDeposito < 0) throw new ArgumentOutOfRangeException(nameof(diasEnDeposito));
    DiasEnDeposito = diasEnDeposito;
    PlazoMaximoDevolucionDias = Validacion.Positivo(plazoMaximoDevolucionDias, nameof(plazoMaximoDevolucionDias));
    DevolverAlAgotarIntentos = devolverAlAgotarIntentos;
  }

  internal void Vincular(ReglasOperativas reglas) => Reglas = reglas;
}
