namespace UltimaMilla.Domain.Envios;

public enum TipoEvento
{
  Admitir,
  Validar,
  Asignar,
  Rechazar,
  Aplazar,
  Reasignar,
  IniciarEntrega,
  EntregaExitosa,
  FalloEntrega,
  Reintentar,
  Devolver,
  Cancelar,
  DevolucionExitosa,
  EventoAnomalo
}
