using UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Repartidor.Dispositivo;

public enum EstadoUbicacion
{
    Disponible,
    // El usuario o el navegador bloquearon el permiso: reintentar no alcanza, hay que habilitarlo.
    PermisoDenegado,
    // Hay permiso pero no llegó una posición (sin señal o tardó demasiado): conviene reintentar.
    SinSenal,
    // El navegador no tiene geolocalización.
    NoSoportada
}

// Resultado de pedir la posición. Separa los motivos de fallo para que la pantalla explique qué hacer.
public sealed record LecturaUbicacion(EstadoUbicacion Estado, GeoPuntoDto? Posicion = null)
{
    // Códigos que devuelve repartidor.js: 0 = posición, 1/2/3 = GeolocationPositionError, -1 = sin soporte.
    public static LecturaUbicacion DesdeNavegador(double[]? datos) => datos switch
    {
        [0, var latitud, var longitud] => new(EstadoUbicacion.Disponible,
            new GeoPuntoDto(Math.Round((decimal)latitud, 6), Math.Round((decimal)longitud, 6))),
        [1] => new(EstadoUbicacion.PermisoDenegado),
        [-1] => new(EstadoUbicacion.NoSoportada),
        // 2 (posición no disponible), 3 (tiempo agotado) o algo inesperado: se trata como falta de señal.
        _ => new(EstadoUbicacion.SinSenal)
    };

    // Mensaje para el repartidor, o null si la posición se obtuvo.
    public string? Mensaje => Estado switch
    {
        EstadoUbicacion.Disponible => null,
        EstadoUbicacion.PermisoDenegado =>
            "La ubicación está bloqueada para esta app. Habilitala en los permisos del sitio del navegador " +
            "y tocá \"Reintentar ubicación\". Sin ubicación no se pueden guardar fotos ni firmas.",
        EstadoUbicacion.NoSoportada =>
            "Este navegador no permite obtener la ubicación. Sin ubicación no se pueden guardar fotos ni firmas.",
        _ =>
            "No hay señal de GPS. Acercate a una ventana o salí al aire libre y tocá \"Reintentar ubicación\"."
    };

    // Reintentar solo sirve si el navegador tiene geolocalización.
    public bool PermiteReintentar => Estado is EstadoUbicacion.PermisoDenegado or EstadoUbicacion.SinSenal;
}
