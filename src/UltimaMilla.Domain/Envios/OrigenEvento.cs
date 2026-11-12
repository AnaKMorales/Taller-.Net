namespace UltimaMilla.Domain.Envios;

public enum OrigenEvento
{
  AppRepartidor,
  Sistema,
  Deposito,
  // El destinatario desde el seguimiento público. No es usuario de la plataforma: el evento no tiene responsable.
  Destinatario
}
