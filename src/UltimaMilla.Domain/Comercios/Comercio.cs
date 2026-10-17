namespace UltimaMilla.Domain.Comercios;

public sealed class Comercio : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Comercio()
  {
    RazonSocial = null!;
    Telefono = null!;
    Email = null!;
  }

  private readonly List<CuentaComercial> _cuentas = [];
  public string RazonSocial { get; private set; }
  public string Telefono { get; private set; }
  public string Email { get; private set; }
  public IReadOnlyCollection<CuentaComercial> CuentasComerciales => _cuentas.AsReadOnly();

  public Comercio(string razonSocial, string telefono, string email)
  {
    RazonSocial = Validacion.Texto(razonSocial, nameof(razonSocial));
    Telefono = Validacion.Texto(telefono, nameof(telefono));
    Email = Validacion.Texto(email, nameof(email));
  }

  internal void Registrar(CuentaComercial cuenta) => _cuentas.Add(cuenta);
}
