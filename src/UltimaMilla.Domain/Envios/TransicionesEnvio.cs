using System.Collections.Frozen;

namespace UltimaMilla.Domain.Envios;

internal static class TransicionesEnvio
{
  private static readonly FrozenDictionary<(EstadoEnvio, TipoEvento), EstadoEnvio> Tabla =
    new Dictionary<(EstadoEnvio, TipoEvento), EstadoEnvio>
    {
      [(EstadoEnvio.Admitido, TipoEvento.Validar)] = EstadoEnvio.EnDeposito,
      [(EstadoEnvio.EnDeposito, TipoEvento.Asignar)] = EstadoEnvio.AsignadoARuta,
      [(EstadoEnvio.AsignadoARuta, TipoEvento.Rechazar)] = EstadoEnvio.EnDeposito,
      [(EstadoEnvio.AsignadoARuta, TipoEvento.Aplazar)] = EstadoEnvio.Reprogramado,
      [(EstadoEnvio.Reprogramado, TipoEvento.Reasignar)] = EstadoEnvio.AsignadoARuta,
      [(EstadoEnvio.AsignadoARuta, TipoEvento.IniciarEntrega)] = EstadoEnvio.EnTransito,
      [(EstadoEnvio.EnTransito, TipoEvento.EntregaExitosa)] = EstadoEnvio.Entregado,
      [(EstadoEnvio.EnTransito, TipoEvento.FalloEntrega)] = EstadoEnvio.NoEntregado,
      [(EstadoEnvio.EnTransito, TipoEvento.EntregaParcial)] = EstadoEnvio.EntregadoParcialmente,
      [(EstadoEnvio.NoEntregado, TipoEvento.Reintentar)] = EstadoEnvio.Reprogramado,
      [(EstadoEnvio.NoEntregado, TipoEvento.Devolver)] = EstadoEnvio.EnDevolucion,
      [(EstadoEnvio.Reprogramado, TipoEvento.Cancelar)] = EstadoEnvio.EnDevolucion,
      [(EstadoEnvio.EnDevolucion, TipoEvento.DevolucionExitosa)] = EstadoEnvio.Devuelto,
      [(EstadoEnvio.EnDeposito, TipoEvento.EventoAnomalo)] = EstadoEnvio.Extraviado,
      [(EstadoEnvio.AsignadoARuta, TipoEvento.EventoAnomalo)] = EstadoEnvio.Extraviado,
      [(EstadoEnvio.EnTransito, TipoEvento.EventoAnomalo)] = EstadoEnvio.Extraviado,
      [(EstadoEnvio.NoEntregado, TipoEvento.EventoAnomalo)] = EstadoEnvio.Extraviado,
      [(EstadoEnvio.Reprogramado, TipoEvento.EventoAnomalo)] = EstadoEnvio.Extraviado,
      [(EstadoEnvio.EnDevolucion, TipoEvento.EventoAnomalo)] = EstadoEnvio.Extraviado
    }.ToFrozenDictionary();

  public static EstadoEnvio Resolver(EstadoEnvio actual, TipoEvento evento)
  {
    if (!Tabla.TryGetValue((actual, evento), out var destino))
      throw new InvalidOperationException($"No se admite {evento} desde {actual}.");
    return destino;
  }
}
