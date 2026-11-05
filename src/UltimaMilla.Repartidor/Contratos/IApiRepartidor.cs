namespace UltimaMilla.Repartidor.Contratos;

// Única puerta hacia el servidor. Hoy la implementa ApiRepartidorFalsa (datos de prueba);
// cuando Web exponga la API, se agrega una implementación con HttpClient y se cambia el registro en Program.cs.
public interface IApiRepartidor
{
    // Devuelve null si las credenciales no son válidas (sin distinguir email inexistente o contraseña).
    Task<SesionRepartidor?> IniciarSesionAsync(string email, string contrasena, CancellationToken ct = default);

    Task<HojaDeRutaDto> ObtenerHojaDeRutaAsync(SesionRepartidor sesion, CancellationToken ct = default);

    // Sin conexión o error del servidor (5xx, timeout): lanza HttpRequestException y la acción se reintenta.
    // Una respuesta de negocio (aceptada, duplicada o rechazada) vuelve como RespuestaAccion.
    Task<RespuestaAccion> EnviarAccionAsync(SesionRepartidor sesion, AccionEntregaDto accion, CancellationToken ct = default);
}
