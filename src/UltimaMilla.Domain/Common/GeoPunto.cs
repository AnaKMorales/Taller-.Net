namespace UltimaMilla.Domain.Common;

public sealed record GeoPunto
{
  public decimal Latitud { get; }
  public decimal Longitud { get; }

  public GeoPunto(decimal latitud, decimal longitud)
  {
    if (latitud is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitud));
    if (longitud is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitud));
    Latitud = latitud;
    Longitud = longitud;
  }
}
