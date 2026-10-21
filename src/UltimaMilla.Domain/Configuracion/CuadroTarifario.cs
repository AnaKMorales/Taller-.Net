namespace UltimaMilla.Domain.Configuracion;

public sealed class CuadroTarifario : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private CuadroTarifario()
  {
    Operador = null!;
  }

  private readonly List<LineaTarifa> _lineas = [];
  public Operador Operador { get; private set; }
  public Guid OperadorId => Operador.Id;
  public int Version { get; private set; }
  public DateTime VigenteDesde { get; private set; }
  public bool EnUso { get; private set; }
  public IReadOnlyCollection<LineaTarifa> Lineas => _lineas.AsReadOnly();

  public CuadroTarifario(Operador operador, int version, DateTime vigenteDesde)
  {
    Operador = Validacion.Requerido(operador, nameof(operador));
    Version = Validacion.Positivo(version, nameof(version));
    Validacion.Fecha(vigenteDesde, nameof(vigenteDesde));
    VigenteDesde = vigenteDesde;
    Operador.Registrar(this);
  }

  internal void Registrar(LineaTarifa linea)
  {
    AsegurarEditable();
    if (_lineas.Any(l => l.ZonaId == linea.ZonaId && l.ModalidadServicio == linea.ModalidadServicio &&
        l.PesoDesde < linea.PesoHasta && linea.PesoDesde < l.PesoHasta))
      throw new InvalidOperationException("Los rangos de peso se superponen para la misma zona y modalidad.");
    _lineas.Add(linea);
  }

  internal void AsegurarEditable()
  {
    if (EnUso) throw new InvalidOperationException("El tarifario en uso es inmutable; cree una nueva version.");
  }

  internal void MarcarEnUso() => EnUso = true;
}
