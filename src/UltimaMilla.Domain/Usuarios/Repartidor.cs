namespace UltimaMilla.Domain.Usuarios;

public sealed class Repartidor : Usuario
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Repartidor()
  {
  }

  private readonly List<Ruta> _rutas = [];
  public IReadOnlyCollection<Ruta> Rutas => _rutas.AsReadOnly();

  public Repartidor(Operador operador, string email) : base(operador, email, RolUsuario.Repartidor) { }
  internal void Registrar(Ruta ruta) => _rutas.Add(ruta);
}
