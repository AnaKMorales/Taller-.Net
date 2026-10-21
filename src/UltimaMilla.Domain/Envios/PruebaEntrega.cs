namespace UltimaMilla.Domain.Envios;

public sealed class PruebaEntrega : Entidad
{
  // Reservado para rehidratar el estado persistido sin ejecutar reglas de alta.
  private PruebaEntrega()
  {
  }

  public TipoPrueba TipoPrueba { get; private set; }
  public GeoPunto? Posicion { get; private set; }
  public DateTime Hora { get; private set; }
  public ArchivoRef? ArchivoRef { get; private set; }
  public string? ValorTexto { get; private set; }
  public IntentoEntrega? Intento { get; private set; }
  public Guid? IntentoId => Intento?.Id;

  public PruebaEntrega(TipoPrueba tipoPrueba, DateTime hora, GeoPunto? posicion,
    ArchivoRef? archivoRef = null, string? valorTexto = null)
  {
    TipoPrueba = Validacion.Enumeracion(tipoPrueba, nameof(tipoPrueba));
    Validacion.Fecha(hora, nameof(hora));
    Hora = hora;
    Posicion = posicion;
    if (tipoPrueba is TipoPrueba.Firma or TipoPrueba.Foto)
      ArchivoRef = Validacion.Requerido(archivoRef, nameof(archivoRef));
    else
      ValorTexto = Validacion.Texto(valorTexto!, nameof(valorTexto));
  }

  internal void Vincular(IntentoEntrega intento) => Intento = intento;
}
