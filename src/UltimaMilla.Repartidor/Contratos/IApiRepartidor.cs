namespace UltimaMilla.Repartidor.Contratos;

// Única puerta hacia el servidor. La implementa Servidor/ApiRepartidorHttp: el login es real;
// hoja de ruta y acciones siguen simuladas hasta que Web exponga sus endpoints.
// Con la sesión vencida o un 401, los métodos que reciben la sesión lanzan SesionExpiradaException.
public interface IApiRepartidor
{
    // Devuelve null si las credenciales no son válidas (sin distinguir email inexistente o contraseña).
    Task<SesionRepartidor?> IniciarSesionAsync(string email, string contrasena, CancellationToken ct = default);

    Task<HojaDeRutaDto> ObtenerHojaDeRutaAsync(SesionRepartidor sesion, CancellationToken ct = default);

    // Sin conexión o error del servidor (5xx, timeout): lanza HttpRequestException y la acción se reintenta.
    // Una respuesta de negocio (aceptada, duplicada o rechazada) vuelve como RespuestaAccion.
    Task<RespuestaAccion> EnviarAccionAsync(SesionRepartidor sesion, AccionEntregaDto accion, CancellationToken ct = default);
}
