namespace UltimaMilla.Domain.Operadores;

public sealed class Operador : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Operador()
  {
    RazonSocial = null!;
  }

  private readonly List<CuentaComercial> _cuentasComerciales = [];
  private readonly List<Usuario> _usuarios = [];
  private readonly List<Vehiculo> _vehiculos = [];
  private readonly List<ZonaCobertura> _zonas = [];
  private readonly List<ReglasOperativas> _reglas = [];
  private readonly List<CuadroTarifario> _cuadros = [];

  public string RazonSocial { get; private set; }
  public IReadOnlyCollection<CuentaComercial> CuentasComerciales => _cuentasComerciales.AsReadOnly();
  public IReadOnlyCollection<Usuario> Usuarios => _usuarios.AsReadOnly();
  public IReadOnlyCollection<Vehiculo> Vehiculos => _vehiculos.AsReadOnly();
  public IReadOnlyCollection<ZonaCobertura> Zonas => _zonas.AsReadOnly();
  public IReadOnlyCollection<ReglasOperativas> VersionesReglas => _reglas.AsReadOnly();
  public IReadOnlyCollection<CuadroTarifario> CuadrosTarifarios => _cuadros.AsReadOnly();

  public Operador(string razonSocial) => RazonSocial = Validacion.Texto(razonSocial, nameof(razonSocial));

  internal void Registrar(CuentaComercial cuenta)
  {
    if (_cuentasComerciales.Any(c => c.ComercioId == cuenta.ComercioId))
      throw new InvalidOperationException("El comercio ya tiene una cuenta con este operador.");
    _cuentasComerciales.Add(cuenta);
  }

  internal void Registrar(Usuario usuario)
  {
    if (_usuarios.Any(u => string.Equals(u.Email, usuario.Email, StringComparison.OrdinalIgnoreCase)))
      throw new InvalidOperationException("El email ya esta registrado en este operador.");
    _usuarios.Add(usuario);
  }

  internal void Registrar(Vehiculo vehiculo)
  {
    if (_vehiculos.Any(v => string.Equals(v.Matricula, vehiculo.Matricula, StringComparison.OrdinalIgnoreCase)))
      throw new InvalidOperationException("La matricula ya esta registrada en este operador.");
    _vehiculos.Add(vehiculo);
  }

  internal void Registrar(ZonaCobertura zona) => _zonas.Add(zona);

  internal void Registrar(ReglasOperativas reglas)
  {
    if (_reglas.Any(r => r.Version == reglas.Version))
      throw new InvalidOperationException("Ya existe esa version de reglas para el operador.");
    _reglas.Add(reglas);
  }

  internal void Registrar(CuadroTarifario cuadro)
  {
    if (_cuadros.Any(c => c.Version == cuadro.Version))
      throw new InvalidOperationException("Ya existe esa version de tarifario para el operador.");
    _cuadros.Add(cuadro);
  }
}
