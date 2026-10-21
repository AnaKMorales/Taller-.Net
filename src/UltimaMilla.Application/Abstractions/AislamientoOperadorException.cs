namespace UltimaMilla.Application.Abstractions;

public sealed class AislamientoOperadorException : InvalidOperationException
{
    public AislamientoOperadorException() : base("La operación requiere un operador válido y datos de su propio ámbito.") { }
}
