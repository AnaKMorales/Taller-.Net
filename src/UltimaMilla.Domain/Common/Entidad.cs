namespace UltimaMilla.Domain.Common;

public abstract class Entidad
{
  public Guid Id { get; private set; } = Guid.NewGuid();
}
