using System.Collections.Frozen;

namespace UltimaMilla.Domain.Configuracion;

public sealed class ReglasOperativas : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private ReglasOperativas()
  {
    Operador = null!;
    TiposPrueba = null!;
    PoliticaDevolucion = null!;
  }

  private readonly List<MotivoNoEntrega> _motivos = [];
  private readonly List<PlazoServicio> _plazos = [];
  public Operador Operador { get; private set; }
  public Guid OperadorId => Operador.Id;
  public int Version { get; private set; }
  public DateTime VigenteDesde { get; private set; }
  public int MaxIntentosEntrega { get; private set; }
  public int PlazoEntreIntentosHoras { get; private set; }
  public IReadOnlySet<TipoPrueba> TiposPrueba { get; private set; }
  public PoliticaDevolucion PoliticaDevolucion { get; private set; }
  public bool PermiteEntregaParcial { get; private set; }
  public AccionBultosPendientes AccionPendientes { get; private set; }
  public bool EnUso { get; private set; }
  public IReadOnlyCollection<MotivoNoEntrega> MotivosNoEntrega => _motivos.AsReadOnly();
  public IReadOnlyCollection<PlazoServicio> PlazosServicio => _plazos.AsReadOnly();

  public ReglasOperativas(Operador operador, int version, DateTime vigenteDesde, int maxIntentosEntrega,
    int plazoEntreIntentosHoras, IEnumerable<TipoPrueba> tiposPrueba, PoliticaDevolucion politicaDevolucion,
    bool permiteEntregaParcial = false, AccionBultosPendientes accionPendientes = AccionBultosPendientes.NuevaVisitaSinRecargo)
  {
    Operador = Validacion.Requerido(operador, nameof(operador));
    Version = Validacion.Positivo(version, nameof(version));
    Validacion.Fecha(vigenteDesde, nameof(vigenteDesde));
    VigenteDesde = vigenteDesde;
    MaxIntentosEntrega = Validacion.Positivo(maxIntentosEntrega, nameof(maxIntentosEntrega));
    if (plazoEntreIntentosHoras < 0) throw new ArgumentOutOfRangeException(nameof(plazoEntreIntentosHoras));
    PlazoEntreIntentosHoras = plazoEntreIntentosHoras;
    ArgumentNullException.ThrowIfNull(tiposPrueba);
    TiposPrueba = tiposPrueba.Select(t => Validacion.Enumeracion(t, nameof(tiposPrueba))).ToFrozenSet();
    PoliticaDevolucion = Validacion.Requerido(politicaDevolucion, nameof(politicaDevolucion));
    if (politicaDevolucion.Reglas is not null) throw new InvalidOperationException("La politica ya pertenece a otras reglas.");
    PermiteEntregaParcial = permiteEntregaParcial;
    AccionPendientes = Validacion.Enumeracion(accionPendientes, nameof(accionPendientes));
    Operador.Registrar(this);
    politicaDevolucion.Vincular(this);
  }

  internal void Registrar(MotivoNoEntrega motivo)
  {
    AsegurarEditable();
    _motivos.Add(motivo);
  }

  internal void Registrar(PlazoServicio plazo)
  {
    AsegurarEditable();
    if (_plazos.Any(p => p.ModalidadServicio == plazo.ModalidadServicio))
      throw new InvalidOperationException("Solo se admite un plazo por modalidad en esta version.");
    _plazos.Add(plazo);
  }

  internal void AsegurarEditable()
  {
    if (EnUso) throw new InvalidOperationException("Las reglas en uso son inmutables; cree una nueva version.");
  }

  internal void MarcarEnUso() => EnUso = true;
}
