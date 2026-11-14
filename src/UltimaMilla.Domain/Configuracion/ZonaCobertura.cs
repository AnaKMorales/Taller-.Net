namespace UltimaMilla.Domain.Configuracion;

public sealed class ZonaCobertura : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private ZonaCobertura()
  {
    Operador = null!;
    Nombre = null!;
    CodigoPostal = null!;
  }

  private readonly List<FranjaHoraria> _franjas = [];
  public Operador Operador { get; private set; }
  public Guid OperadorId => Operador.Id;
  public string Nombre { get; private set; }
  public string CodigoPostal { get; private set; }
  public IReadOnlyCollection<FranjaHoraria> Franjas => _franjas.AsReadOnly();

  public ZonaCobertura(Operador operador, string nombre, string codigoPostal, IEnumerable<FranjaHoraria> franjas)
  {
    Operador = Validacion.Requerido(operador, nameof(operador));
    Nombre = Validacion.Texto(nombre, nameof(nombre));
    CodigoPostal = Validacion.Texto(codigoPostal, nameof(codigoPostal));
    ArgumentNullException.ThrowIfNull(franjas);
    _franjas = franjas.ToList();
    if (_franjas.Count == 0 || _franjas.Any(f => f is null))
      throw new ArgumentException("La zona requiere al menos una franja.", nameof(franjas));
    if (_franjas.Select(f => f.Id).Distinct().Count() != _franjas.Count || _franjas.Any(f => f.Zona is not null))
      throw new ArgumentException("Las franjas no pueden repetirse ni pertenecer a otra zona.", nameof(franjas));
    foreach (var franja in _franjas) franja.Vincular(this);
    Operador.Registrar(this);
  }

  public void AgregarFranja(FranjaHoraria franja)
  {
    ArgumentNullException.ThrowIfNull(franja);
    if (franja.Zona is not null)
      throw new InvalidOperationException("La franja ya pertenece a una zona.");
    franja.Vincular(this);
    _franjas.Add(franja);
  }
}
