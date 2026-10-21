namespace UltimaMilla.Domain.Common;

// Referencia al archivo; el dominio no almacena ni lee su contenido.
public sealed record ArchivoRef
{
  public string Ubicacion { get; }

  public ArchivoRef(string ubicacion)
  {
    Ubicacion = Validacion.Texto(ubicacion, nameof(ubicacion));
  }
}
