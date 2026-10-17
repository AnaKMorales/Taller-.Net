namespace UltimaMilla.Domain.Comercios;

// Relacion entre un comercio y un operador; delimita los datos de ese cliente.
public sealed class CuentaComercial : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private CuentaComercial()
  {
    Operador = null!;
    Comercio = null!;
  }

  private readonly List<Envio> _envios = [];
  private readonly List<Usuario> _usuarios = [];
  private readonly List<SuscripcionAviso> _suscripciones = [];
  public Operador Operador { get; private set; }
  public Guid OperadorId => Operador.Id;
  public Comercio Comercio { get; private set; }
  public Guid ComercioId => Comercio.Id;
  public IReadOnlyCollection<Envio> Envios => _envios.AsReadOnly();
  public IReadOnlyCollection<Usuario> Usuarios => _usuarios.AsReadOnly();
  public IReadOnlyCollection<SuscripcionAviso> Suscripciones => _suscripciones.AsReadOnly();

  public CuentaComercial(Operador operador, Comercio comercio)
  {
    Operador = Validacion.Requerido(operador, nameof(operador));
    Comercio = Validacion.Requerido(comercio, nameof(comercio));
    Operador.Registrar(this);
    Comercio.Registrar(this);
  }

  internal void Registrar(Envio envio) => _envios.Add(envio);
  internal void Registrar(Usuario usuario) => _usuarios.Add(usuario);
  internal void Registrar(SuscripcionAviso suscripcion) => _suscripciones.Add(suscripcion);
}
