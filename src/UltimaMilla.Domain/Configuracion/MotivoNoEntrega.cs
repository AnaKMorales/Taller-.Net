namespace UltimaMilla.Domain.Configuracion;

public sealed class MotivoNoEntrega : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private MotivoNoEntrega()
  {
    Reglas = null!;
    Nombre = null!;
    Descripcion = null!;
  }

  public ReglasOperativas Reglas { get; private set; }
  public Guid ReglasId => Reglas.Id;
  public Guid OperadorId => Reglas.OperadorId;
  public string Nombre { get; private set; }
  public string Descripcion { get; private set; }
  public Responsable Responsable { get; private set; }
  public bool RequiereEvidencia { get; private set; }
  public bool EsReintentable { get; private set; }
  public bool Activo { get; private set; }

  public MotivoNoEntrega(ReglasOperativas reglas, string nombre, string descripcion, Responsable responsable,
    bool requiereEvidencia, bool esReintentable, bool activo = true)
  {
    Reglas = Validacion.Requerido(reglas, nameof(reglas));
    Nombre = Validacion.Texto(nombre, nameof(nombre));
    Descripcion = Validacion.Texto(descripcion, nameof(descripcion));
    Responsable = Validacion.Enumeracion(responsable, nameof(responsable));
    RequiereEvidencia = requiereEvidencia;
    EsReintentable = esReintentable;
    Activo = activo;
    Reglas.Registrar(this);
  }
}
