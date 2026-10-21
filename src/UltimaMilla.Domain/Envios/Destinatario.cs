namespace UltimaMilla.Domain.Envios;

public sealed class Destinatario : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private Destinatario()
  {
    Nombre = null!;
    Telefono = null!;
    Email = null!;
    Direccion = null!;
  }

  private readonly List<Notificacion> _notificaciones = [];
  public string Nombre { get; private set; }
  public string Telefono { get; private set; }
  public string Email { get; private set; }
  public Direccion Direccion { get; private set; }
  public Guid DireccionId => Direccion.Id;
  public Envio? Envio { get; private set; }
  public IReadOnlyCollection<Notificacion> Notificaciones => _notificaciones.AsReadOnly();

  public Destinatario(string nombre, string telefono, string email, Direccion direccion)
  {
    Nombre = Validacion.Texto(nombre, nameof(nombre));
    Telefono = Validacion.Texto(telefono, nameof(telefono));
    Email = Validacion.Texto(email, nameof(email));
    Direccion = Validacion.Requerido(direccion, nameof(direccion));
    if (direccion.Destinatario is not null) throw new InvalidOperationException("La direccion ya pertenece a otro destinatario.");
    direccion.Vincular(this);
  }

  internal void Vincular(Envio envio) => Envio = envio;
  internal void Registrar(Notificacion notificacion) => _notificaciones.Add(notificacion);
}
