namespace UltimaMilla.Domain.Envios;

public enum EstadoEnvio
{
  Admitido,
  EnDeposito,
  AsignadoARuta,
  EnTransito,
  Entregado,
  NoEntregado,
  Reprogramado,
  EnDevolucion,
  Devuelto,
  Extraviado
}
