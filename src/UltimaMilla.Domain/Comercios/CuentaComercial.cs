namespace UltimaMilla.Domain.Comercios;

// Relacion entre un comercio y un operador; delimita los datos de ese cliente.
public sealed class CuentaComercial : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private CuentaComercial()
  {
    Operador = null!;
    Comercio = null!;
    RazonSocial = Telefono = Email = null!;
  }

  private readonly List<Envio> _envios = [];
  private readonly List<Usuario> _usuarios = [];
  private readonly List<SuscripcionAviso> _suscripciones = [];
  public Operador Operador { get; private set; }
  public Guid OperadorId => Operador.Id;
  public Comercio Comercio { get; private set; }
  public Guid ComercioId => Comercio.Id;
  public string RazonSocial { get; private set; }
  public string Telefono { get; private set; }
  public string Email { get; private set; }
  public bool Activa { get; private set; } = true;
  public Guid Version { get; private set; } = Guid.NewGuid();
  public IReadOnlyCollection<Envio> Envios => _envios.AsReadOnly();
  public IReadOnlyCollection<Usuario> Usuarios => _usuarios.AsReadOnly();
  public IReadOnlyCollection<SuscripcionAviso> Suscripciones => _suscripciones.AsReadOnly();

  public CuentaComercial(Operador operador, Comercio comercio)
  {
    Operador = Validacion.Requerido(operador, nameof(operador));
    Comercio = Validacion.Requerido(comercio, nameof(comercio));
    RazonSocial = comercio.RazonSocial;
    Telefono = comercio.Telefono;
    Email = comercio.Email;
    Operador.Registrar(this);
    Comercio.Registrar(this);
  }

  internal void Registrar(Envio envio) => _envios.Add(envio);
  public void ModificarDatos(string razonSocial, string telefono, string email)
  {
    if (!Activa) throw new InvalidOperationException("La cuenta comercial está dada de baja.");
    var nombre = Validacion.Texto(razonSocial, nameof(razonSocial));
    var tel = Validacion.Texto(telefono, nameof(telefono));
    var contacto = Validacion.Texto(email, nameof(email));
    RazonSocial = nombre; Telefono = tel; Email = contacto;
    Version = Guid.NewGuid();
  }

  public void DarBaja()
  {
    if (!Activa) return;
    Activa = false; Version = Guid.NewGuid();
  }
  internal void Registrar(Usuario usuario) => _usuarios.Add(usuario);
  internal void Registrar(SuscripcionAviso suscripcion) => _suscripciones.Add(suscripcion);
}
