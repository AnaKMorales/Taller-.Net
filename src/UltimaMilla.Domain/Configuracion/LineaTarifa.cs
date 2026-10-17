namespace UltimaMilla.Domain.Configuracion;

public sealed class LineaTarifa : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private LineaTarifa()
  {
    Cuadro = null!;
    Zona = null!;
  }

  private readonly List<AjusteTarifario> _ajustes = [];
  public CuadroTarifario Cuadro { get; private set; }
  public Guid CuadroId => Cuadro.Id;
  public ZonaCobertura Zona { get; private set; }
  public Guid ZonaId => Zona.Id;
  public ModalidadServicio ModalidadServicio { get; private set; }
  public decimal PesoDesde { get; private set; }
  public decimal PesoHasta { get; private set; }
  public decimal VolumenHasta { get; private set; }
  public decimal Precio { get; private set; }
  public IReadOnlyCollection<AjusteTarifario> Ajustes => _ajustes.AsReadOnly();

  public LineaTarifa(CuadroTarifario cuadro, ZonaCobertura zona, ModalidadServicio modalidadServicio,
    decimal pesoDesde, decimal pesoHasta, decimal volumenHasta, decimal precio)
  {
    Cuadro = Validacion.Requerido(cuadro, nameof(cuadro));
    Zona = Validacion.Requerido(zona, nameof(zona));
    Validacion.MismoOperador(cuadro.OperadorId, zona.OperadorId);
    ModalidadServicio = Validacion.Enumeracion(modalidadServicio, nameof(modalidadServicio));
    PesoDesde = Validacion.NoNegativo(pesoDesde, nameof(pesoDesde));
    if (pesoHasta <= pesoDesde) throw new ArgumentOutOfRangeException(nameof(pesoHasta));
    PesoHasta = pesoHasta;
    VolumenHasta = Validacion.Positivo(volumenHasta, nameof(volumenHasta));
    Precio = Validacion.NoNegativo(precio, nameof(precio));
    Cuadro.Registrar(this);
  }

  internal void Registrar(AjusteTarifario ajuste)
  {
    Cuadro.AsegurarEditable();
    _ajustes.Add(ajuste);
  }
}
