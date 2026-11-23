namespace UltimaMilla.Application.Mensajeria;

// Fallo atribuible a esta publicación (por ejemplo, el broker devolvió el mensaje sin ruta).
// Una caída de conexión o una confirmación incierta conserva su excepción original.
public sealed class MensajeNoPublicableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
