namespace UltimaMilla.Domain.Despacho;

public sealed class Vehiculo : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Vehiculo()
  {
    Operador = null!;
    Matricula = null!;
  }

  private readonly List<Ruta> _rutas = [];
  public Operador Operador { get; private set; }
  public Guid OperadorId => Operador.Id;
  public string Matricula { get; private set; }
  public decimal CapacidadPesoKg { get; private set; }
  public decimal CapacidadVolumenM3 { get; private set; }
  public IReadOnlyCollection<Ruta> Rutas => _rutas.AsReadOnly();

  public Vehiculo(Operador operador, string matricula, decimal capacidadPesoKg, decimal capacidadVolumenM3)
  {
    Operador = Validacion.Requerido(operador, nameof(operador));
    Matricula = Validacion.Texto(matricula, nameof(matricula));
    CapacidadPesoKg = Validacion.Positivo(capacidadPesoKg, nameof(capacidadPesoKg));
    CapacidadVolumenM3 = Validacion.Positivo(capacidadVolumenM3, nameof(capacidadVolumenM3));
    Operador.Registrar(this);
  }

  internal void Registrar(Ruta ruta) => _rutas.Add(ruta);
}
