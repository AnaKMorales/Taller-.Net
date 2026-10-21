namespace UltimaMilla.Domain.Common;

internal static class Validacion
{
  public static string Texto(string valor, string parametro)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(valor, parametro);
    return valor.Trim();
  }

  public static T Requerido<T>(T? valor, string parametro) where T : class
  {
    ArgumentNullException.ThrowIfNull(valor, parametro);
    return valor;
  }

  public static decimal Positivo(decimal valor, string parametro)
  {
    if (valor <= 0) throw new ArgumentOutOfRangeException(parametro, "Debe ser mayor que cero.");
    return valor;
  }

  public static int Positivo(int valor, string parametro)
  {
    if (valor <= 0) throw new ArgumentOutOfRangeException(parametro, "Debe ser mayor que cero.");
    return valor;
  }

  public static decimal NoNegativo(decimal valor, string parametro)
  {
    if (valor < 0) throw new ArgumentOutOfRangeException(parametro, "No puede ser negativo.");
    return valor;
  }

  public static T Enumeracion<T>(T valor, string parametro) where T : struct, Enum
  {
    if (!Enum.IsDefined(valor)) throw new ArgumentOutOfRangeException(parametro, "Valor no definido.");
    return valor;
  }

  public static void MismoOperador(Guid esperado, Guid recibido)
  {
    if (esperado != recibido) throw new InvalidOperationException("La relacion pertenece a otro operador.");
  }

  public static void Fecha(DateTime valor, string parametro)
  {
    if (valor == default) throw new ArgumentException("La fecha es obligatoria.", parametro);
  }
}
