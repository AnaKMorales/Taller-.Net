namespace UltimaMilla.Domain.Operadores;

public sealed class Operador : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Operador()
  {
    RazonSocial = Marca = null!;
  }

  private readonly List<CuentaComercial> _cuentasComerciales = [];
  private readonly List<Usuario> _usuarios = [];
  private readonly List<Vehiculo> _vehiculos = [];
  private readonly List<ZonaCobertura> _zonas = [];
  private readonly List<ReglasOperativas> _reglas = [];
  private readonly List<CuadroTarifario> _cuadros = [];

  public string RazonSocial { get; private set; }
  public string Marca { get; private set; }
  public string ColorPrimario { get; private set; } = "#1E40AF";
  public string ColorSecundario { get; private set; } = "#0F172A";
  public string? TelefonoContacto { get; private set; }
  public string? EmailContacto { get; private set; }
  public string? LogoUrl { get; private set; }
  public Guid VersionIdentidad { get; private set; } = Guid.NewGuid();
  public IReadOnlyCollection<CuentaComercial> CuentasComerciales => _cuentasComerciales.AsReadOnly();
  public IReadOnlyCollection<Usuario> Usuarios => _usuarios.AsReadOnly();
  public IReadOnlyCollection<Vehiculo> Vehiculos => _vehiculos.AsReadOnly();
  public IReadOnlyCollection<ZonaCobertura> Zonas => _zonas.AsReadOnly();
  public IReadOnlyCollection<ReglasOperativas> VersionesReglas => _reglas.AsReadOnly();
  public IReadOnlyCollection<CuadroTarifario> CuadrosTarifarios => _cuadros.AsReadOnly();

  public Operador(string razonSocial)
  {
    RazonSocial = Validacion.Texto(razonSocial, nameof(razonSocial));
    Marca = RazonSocial;
  }

  public void ConfigurarIdentidad(string marca, string colorPrimario, string colorSecundario,
    string? telefono, string? email, string? logoUrl)
  {
    var nombre = Validacion.Texto(marca, nameof(marca));
    if (nombre.Length > 200) throw new ArgumentException("La marca admite hasta 200 caracteres.");
    var primario = Color(colorPrimario); var secundario = Color(colorSecundario);
    var tel = Opcional(telefono, 50); var correo = Opcional(email, 254)?.ToLowerInvariant();
    if (correo is not null && (!System.Net.Mail.MailAddress.TryCreate(correo, out var direccion) || direccion.Address != correo))
      throw new ArgumentException("El email de contacto debe ser una dirección válida.");
    var logo = Opcional(logoUrl, 2048);
    if (logo is not null && (!Uri.TryCreate(logo, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
        !string.IsNullOrEmpty(uri.UserInfo)))
      throw new ArgumentException("El logo debe ser una URL HTTPS sin credenciales.");
    Marca = nombre; ColorPrimario = primario; ColorSecundario = secundario;
    TelefonoContacto = tel; EmailContacto = correo; LogoUrl = logo;
    VersionIdentidad = Guid.NewGuid();
  }

  private static string Color(string color)
  {
    var valor = color?.Trim();
    if (valor is null || valor.Length != 7 || valor[0] != '#' || valor.Skip(1).Any(c => !Uri.IsHexDigit(c)))
      throw new ArgumentException("Los colores deben tener formato #RRGGBB.");
    return valor.ToUpperInvariant();
  }

  private static string? Opcional(string? texto, int limite)
  {
    if (string.IsNullOrWhiteSpace(texto)) return null;
    var valor = texto.Trim();
    if (valor.Length > limite) throw new ArgumentException($"El texto admite hasta {limite} caracteres.");
    return valor;
  }

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
