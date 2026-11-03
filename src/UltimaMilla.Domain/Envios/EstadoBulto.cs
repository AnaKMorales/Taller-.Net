namespace UltimaMilla.Domain.Envios;

public enum EstadoBulto
{
  Admitido = 0,
  EnDeposito = 1,
  AsignadoARuta = 2,
  EnTransito = 3,
  Entregado = 4,
  NoEntregado = 5,
  Reprogramado = 6,
  EnDevolucion = 7,
  Devuelto = 8,
  Extraviado = 9
}
