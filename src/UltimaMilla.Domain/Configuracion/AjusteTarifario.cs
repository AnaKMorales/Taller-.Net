namespace UltimaMilla.Domain.Configuracion;

public sealed class AjusteTarifario : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private AjusteTarifario()
  {
    Linea = null!;
  }

  public LineaTarifa Linea { get; private set; }
  public Guid LineaId => Linea.Id;
  public TipoAjuste Tipo { get; private set; }
  public decimal ValorPorcentaje { get; private set; }

  public AjusteTarifario(LineaTarifa linea, TipoAjuste tipo, decimal valorPorcentaje)
  {
    Linea = Validacion.Requerido(linea, nameof(linea));
    Tipo = Validacion.Enumeracion(tipo, nameof(tipo));
    ValorPorcentaje = Validacion.Positivo(valorPorcentaje, nameof(valorPorcentaje));
    if (tipo == TipoAjuste.Bonificacion && valorPorcentaje > 100)
      throw new ArgumentOutOfRangeException(nameof(valorPorcentaje), "La bonificacion no puede superar el 100%.");
    Linea.Registrar(this);
  }
}
