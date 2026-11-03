using System.Collections.Frozen;

namespace UltimaMilla.Domain.Envios;

// Resumen sin efectos secundarios. El detalle de cada bulto conserva su situación exacta.
public static class EstadoConjuntoEnvio
{
  private static readonly FrozenDictionary<EstadoBulto, EstadoEnvio> Equivalencias =
    new Dictionary<EstadoBulto, EstadoEnvio>
    {
      [EstadoBulto.Admitido] = EstadoEnvio.Admitido,
      [EstadoBulto.EnDeposito] = EstadoEnvio.EnDeposito,
      [EstadoBulto.AsignadoARuta] = EstadoEnvio.AsignadoARuta,
      [EstadoBulto.EnTransito] = EstadoEnvio.EnTransito,
      [EstadoBulto.Entregado] = EstadoEnvio.Entregado,
      [EstadoBulto.NoEntregado] = EstadoEnvio.NoEntregado,
      [EstadoBulto.Reprogramado] = EstadoEnvio.Reprogramado,
      [EstadoBulto.EnDevolucion] = EstadoEnvio.EnDevolucion,
      [EstadoBulto.Devuelto] = EstadoEnvio.Devuelto,
      [EstadoBulto.Extraviado] = EstadoEnvio.Extraviado
    }.ToFrozenDictionary();

  // Sin entregados, se prioriza incidencia, devolución y operación pendiente sobre bultos cerrados.
  private static readonly IReadOnlyList<EstadoBulto> Prioridad = new List<EstadoBulto> { EstadoBulto.Extraviado, EstadoBulto.EnDevolucion,
    EstadoBulto.EnTransito, EstadoBulto.AsignadoARuta, EstadoBulto.Reprogramado, EstadoBulto.NoEntregado,
    EstadoBulto.EnDeposito, EstadoBulto.Admitido, EstadoBulto.Devuelto }.AsReadOnly();

  public static EstadoEnvio Calcular(IEnumerable<EstadoBulto> estados)
  {
    ArgumentNullException.ThrowIfNull(estados);
    var presentes = estados.Select(e => Validacion.Enumeracion(e, nameof(estados))).ToHashSet();
    if (presentes.Count == 0) throw new ArgumentException("Se requieren los estados de todos los bultos.", nameof(estados));
    if (presentes.Count == 1) return Equivalencias[presentes.Single()];
    if (presentes.Contains(EstadoBulto.Entregado)) return EstadoEnvio.EntregadoParcialmente;
    return Equivalencias[Prioridad.First(presentes.Contains)];
  }
}
