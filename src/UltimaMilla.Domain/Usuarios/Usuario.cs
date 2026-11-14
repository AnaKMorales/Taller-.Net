namespace UltimaMilla.Domain.Usuarios;

public class Usuario : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  protected Usuario()
  {
    Operador = null!;
    Email = null!;
  }

  private readonly List<CuentaComercial> _cuentas = [];
  public Operador Operador { get; private set; }
  public Guid OperadorId => Operador.Id;
  public string Email { get; private set; }
  public RolUsuario Rol { get; private set; }
  public bool Activo { get; private set; } = true;
  public IReadOnlyCollection<CuentaComercial> CuentasComerciales => _cuentas.AsReadOnly();

  public Usuario(Operador operador, string email, RolUsuario rol)
  {
    Operador = Validacion.Requerido(operador, nameof(operador));
    Email = Validacion.Texto(email, nameof(email));
    Rol = Validacion.Enumeracion(rol, nameof(rol));
    if (GetType() == typeof(Usuario) && rol == RolUsuario.Repartidor)
      throw new ArgumentException("Para ese perfil se debe crear un Repartidor.", nameof(rol));
    Operador.Registrar(this);
  }

  public void AsignarCuentaComercial(CuentaComercial cuenta)
  {
    ArgumentNullException.ThrowIfNull(cuenta);
    Validacion.MismoOperador(OperadorId, cuenta.OperadorId);
    if (Rol != RolUsuario.Comercio) throw new InvalidOperationException("El usuario no es de comercio.");
    if (_cuentas.Any(c => c.Id == cuenta.Id)) throw new InvalidOperationException("La cuenta ya esta asignada.");
    _cuentas.Add(cuenta);
    cuenta.Registrar(this);
  }

  public void Desactivar() => Activo = false;

  public void ActualizarEmail(string email)
  {
    if (!Activo) throw new InvalidOperationException("No se puede modificar un usuario inactivo.");
    Email = Validacion.Texto(email, nameof(email));
  }
}
