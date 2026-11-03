using System.Collections.Frozen;

namespace UltimaMilla.Domain.Envios;

// Política pura: consultar una transición no modifica un bulto ni evita las reglas del agregado.
public static class TransicionesBulto
{
  private static readonly FrozenDictionary<(EstadoBulto, TipoEvento), EstadoBulto> Tabla =
    new Dictionary<(EstadoBulto, TipoEvento), EstadoBulto>
    {
      [(EstadoBulto.Admitido, TipoEvento.Validar)] = EstadoBulto.EnDeposito,
      [(EstadoBulto.EnDeposito, TipoEvento.Asignar)] = EstadoBulto.AsignadoARuta,
      [(EstadoBulto.AsignadoARuta, TipoEvento.Rechazar)] = EstadoBulto.EnDeposito,
      [(EstadoBulto.AsignadoARuta, TipoEvento.Aplazar)] = EstadoBulto.Reprogramado,
      [(EstadoBulto.Reprogramado, TipoEvento.Reasignar)] = EstadoBulto.AsignadoARuta,
      [(EstadoBulto.AsignadoARuta, TipoEvento.IniciarEntrega)] = EstadoBulto.EnTransito,
      [(EstadoBulto.EnTransito, TipoEvento.EntregaExitosa)] = EstadoBulto.Entregado,
      [(EstadoBulto.EnTransito, TipoEvento.FalloEntrega)] = EstadoBulto.NoEntregado,
      [(EstadoBulto.NoEntregado, TipoEvento.Reintentar)] = EstadoBulto.Reprogramado,
      [(EstadoBulto.NoEntregado, TipoEvento.Devolver)] = EstadoBulto.EnDevolucion,
      [(EstadoBulto.Reprogramado, TipoEvento.Cancelar)] = EstadoBulto.EnDevolucion,
      [(EstadoBulto.EnDevolucion, TipoEvento.DevolucionExitosa)] = EstadoBulto.Devuelto,
      [(EstadoBulto.EnDeposito, TipoEvento.EventoAnomalo)] = EstadoBulto.Extraviado,
      [(EstadoBulto.AsignadoARuta, TipoEvento.EventoAnomalo)] = EstadoBulto.Extraviado,
      [(EstadoBulto.EnTransito, TipoEvento.EventoAnomalo)] = EstadoBulto.Extraviado,
      [(EstadoBulto.NoEntregado, TipoEvento.EventoAnomalo)] = EstadoBulto.Extraviado,
      [(EstadoBulto.Reprogramado, TipoEvento.EventoAnomalo)] = EstadoBulto.Extraviado,
      [(EstadoBulto.EnDevolucion, TipoEvento.EventoAnomalo)] = EstadoBulto.Extraviado
    }.ToFrozenDictionary();

  public static EstadoBulto Resolver(EstadoBulto actual, TipoEvento evento) =>
    Tabla.TryGetValue((actual, evento), out var siguiente) ? siguiente :
      throw new InvalidOperationException($"No se admite {evento} desde el estado de bulto {actual}.");
}
