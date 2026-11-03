namespace UltimaMilla.Domain.Envios;

public sealed record ResultadoBultoEntrega(Guid BultoId, ResultadoIntento Resultado,
  MotivoNoEntrega? Motivo, IReadOnlyList<PruebaEntrega> Pruebas);
