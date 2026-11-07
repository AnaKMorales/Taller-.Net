namespace UltimaMilla.Application.Abstractions;

// Contrato independiente de EF y PostgreSQL para los futuros casos de uso y la API.
public class ConflictoConcurrenciaException(Exception innerException)
    : InvalidOperationException(
        "Los datos cambiaron mientras se realizaba la operación. Vuelve a consultarlos antes de intentarlo nuevamente.",
        innerException);
