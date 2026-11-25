namespace UltimaMilla.Repartidor.Contratos;

// Respuesta de activarAvisos (wwwroot/js/repartidor.js): la suscripción o el motivo del error.
public sealed record ResultadoSuscripcionPush(string? Endpoint, string? P256dh, string? Auth, string? Error);

// Cuerpos y respuestas de /api/repartidor/avisos (Web: Seguridad/AvisosPushRepartidorEndpoints.cs).
public sealed record ClavePushDto(string ClavePublica);
public sealed record SuscripcionPushDto(string Endpoint, string P256dh, string Auth);
public sealed record BajaSuscripcionPushDto(string Endpoint);

// Registro del dispositivo para los avisos con la app cerrada. Aparte de IApiRepartidor: es un canal auxiliar.
public interface IApiAvisos
{
    // null si el servidor no tiene Web Push configurado.
    Task<string?> ObtenerClavePushAsync(SesionRepartidor sesion, CancellationToken ct = default);
    Task RegistrarSuscripcionPushAsync(SesionRepartidor sesion, SuscripcionPushDto suscripcion, CancellationToken ct = default);
    Task QuitarSuscripcionPushAsync(SesionRepartidor sesion, string endpoint, CancellationToken ct = default);
}
