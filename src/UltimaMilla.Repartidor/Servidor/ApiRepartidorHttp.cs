using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Simulacion;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Servidor;

// Cliente de la API del repartidor en Web (ver Web/Seguridad/ApiRepartidor.cs).
// - Login: real (POST /api/repartidor/sesion + GET /api/repartidor/sesion con el token).
// - Hoja de ruta y acciones: Web todavía no tiene esos endpoints. Antes de usar la simulación se
//   valida el token contra el servidor, así el vencimiento y los 401 ya se manejan como serán.
//   Cuando existan los endpoints, se reemplaza la llamada a "simulada" por el pedido HTTP correspondiente.
public sealed class ApiRepartidorHttp(HttpClient http, ApiRepartidorFalsa simulada, TimeProvider reloj) : IApiRepartidor
{
    private const string RutaSesion = "api/repartidor/sesion";

    public async Task<SesionRepartidor?> IniciarSesionAsync(string email, string contrasena, CancellationToken ct = default)
    {
        using var respuesta = await http.PostAsJsonAsync(RutaSesion, new SolicitudLogin(email, contrasena),
            JsonRepartidor.Default.SolicitudLogin, ct);
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized) return null;
        respuesta.EnsureSuccessStatusCode();

        var token = await respuesta.Content.ReadFromJsonAsync(JsonRepartidor.Default.TokenAcceso, ct)
                    ?? throw new HttpRequestException("El servidor no devolvió un token.");
        // La hora de vencimiento se calcula con el reloj del dispositivo al recibir el token.
        var expiraEn = reloj.GetUtcNow().AddSeconds(token.ExpiresIn);

        // El perfil (id y email) sale del servidor a partir del token, no de lo que tipeó el usuario.
        var perfil = await ObtenerPerfilAsync(token.AccessToken, ct);
        return new SesionRepartidor(token.AccessToken, perfil.UsuarioId, perfil.Email, expiraEn);
    }

    public async Task<HojaDeRutaDto> ObtenerHojaDeRutaAsync(SesionRepartidor sesion, CancellationToken ct = default)
    {
        await VerificarSesionAsync(sesion, ct);
        return await simulada.ObtenerHojaDeRutaAsync(ct);
    }

    public async Task<RespuestaAccion> EnviarAccionAsync(SesionRepartidor sesion, AccionEntregaDto accion, CancellationToken ct = default)
    {
        await VerificarSesionAsync(sesion, ct);
        return await simulada.EnviarAccionAsync(accion, ct);
    }

    private async Task VerificarSesionAsync(SesionRepartidor sesion, CancellationToken ct)
    {
        // Con el token vencido no tiene sentido llamar al servidor.
        if (sesion.Vencida(reloj.GetUtcNow())) throw new SesionExpiradaException();
        await ObtenerPerfilAsync(sesion.Token, ct);
    }

    private async Task<PerfilRepartidor> ObtenerPerfilAsync(string token, CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, RutaSesion);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var respuesta = await http.SendAsync(pedido, ct);
        // 401: token vencido, inválido o usuario desactivado. 403: el token no es de un repartidor.
        if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new SesionExpiradaException();
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync(JsonRepartidor.Default.PerfilRepartidor, ct)
               ?? throw new HttpRequestException("El servidor no devolvió el perfil.");
    }
}
