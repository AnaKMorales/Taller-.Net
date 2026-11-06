using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Simulacion;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Servidor;

// Cliente de sesión, hojas e intentos del repartidor.
public sealed class ApiRepartidorHttp(HttpClient http, ApiRepartidorFalsa simulada, TimeProvider reloj) : IApiRepartidor
{
    private const string RutaSesion = "api/repartidor/sesion";
    private const string RutaHojas = "api/repartidor/rutas";

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

    public async Task<HojaDeRutaDto> ObtenerHojaDeRutaAsync(SesionRepartidor sesion, Guid? rutaPreferida = null,
        CancellationToken ct = default)
    {
        VerificarVigencia(sesion);
        try
        {
            using var listado = await ConsultarAsync(RutaHojas, sesion.Token, ct);
            listado.EnsureSuccessStatusCode();
            var rutas = await listado.Content.ReadFromJsonAsync(JsonRepartidor.Default.RutaDisponibleDtoArray, ct)
                ?? throw new HttpRequestException("El servidor no devolvió las rutas disponibles.");
            var disponibles = rutas.OrderByDescending(r => r.ParadasPendientes > 0)
                .ThenByDescending(r => r.DespachadaEn).ThenBy(r => r.RutaId).ToList();

            // Una ruta puede quedar rendida entre el listado y el detalle. En ese caso se prueba la siguiente.
            foreach (var ruta in disponibles.OrderByDescending(r => r.RutaId == rutaPreferida).ToArray())
            {
                using var detalle = await ConsultarAsync($"{RutaHojas}/{ruta.RutaId}", sesion.Token, ct);
                if (detalle.StatusCode == HttpStatusCode.NotFound)
                {
                    disponibles.Remove(ruta);
                    continue;
                }

                detalle.EnsureSuccessStatusCode();
                var hoja = await detalle.Content.ReadFromJsonAsync(JsonRepartidor.Default.HojaRutaApiDto, ct)
                    ?? throw new HttpRequestException("El servidor no devolvió la hoja de ruta.");
                var paradas = hoja.Paradas.Select(p =>
                {
                    var direccion = p.Destinatario.Direccion;
                    return new ParadaDto(p.EnvioId, p.Orden, p.Destinatario.Nombre,
                        $"{direccion.Calle} {direccion.Numero}, {direccion.Localidad} ({direccion.CodigoPostal})",
                        p.VentanaDesde, p.VentanaHasta, p.EstadoEnvio, p.ParadaId, p.VersionEsperadaEnvio, p.Reglas, p.Bultos);
                }).ToArray();
                return new HojaDeRutaDto(hoja.RutaId, reloj.GetUtcNow(), paradas, [], [], hoja.Version, disponibles);
            }

            return new HojaDeRutaDto(Guid.Empty, reloj.GetUtcNow(), [], [], [], RutasDisponibles: []);
        }
        catch (JsonException error)
        {
            throw new HttpRequestException("El servidor devolvió una hoja de ruta inválida.", error);
        }
        catch (TaskCanceledException error) when (!ct.IsCancellationRequested)
        {
            throw new HttpRequestException("El servidor tardó demasiado en devolver la hoja de ruta.", error);
        }
    }

    public async Task<RespuestaAccion> EnviarAccionAsync(SesionRepartidor sesion, AccionEntregaDto accion, CancellationToken ct = default)
    {
        VerificarVigencia(sesion);
        if (accion.Intento is { } intento)
            return await EnviarIntentoAsync(sesion, intento, ct);

        if (!ApiRepartidorFalsa.EsEnvioDePrueba(accion.EnvioId))
            return new(ResultadoAccion.Rechazada, "Descargá la hoja y registrá el resultado por bulto.");
        await ObtenerPerfilAsync(sesion.Token, ct);
        return await simulada.EnviarAccionAsync(accion, ct);
    }

    private async Task<RespuestaAccion> EnviarIntentoAsync(SesionRepartidor sesion, IntentoPorBultoDto intento, CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Post, "api/repartidor/intentos");
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sesion.Token);
        pedido.Content = JsonContent.Create(intento, JsonRepartidor.Default.IntentoPorBultoDto);
        using var respuesta = await http.SendAsync(pedido, ct);
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
            throw new SesionExpiradaException();

        if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or
            HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.RequestEntityTooLarge or
            HttpStatusCode.UnprocessableEntity)
        {
            var problema = await LeerProblemaAsync(respuesta, ct);
            var detalle = problema?.Detail ?? problema?.Title ?? "El servidor rechazó la visita.";
            if (respuesta.StatusCode == HttpStatusCode.Conflict)
                detalle += " Actualizá la hoja antes de volver a registrar.";
            return new(ResultadoAccion.Rechazada, detalle);
        }

        respuesta.EnsureSuccessStatusCode();
        IntentoConfirmadoDto? confirmado;
        try
        {
            confirmado = await respuesta.Content.ReadFromJsonAsync(JsonRepartidor.Default.IntentoConfirmadoDto, ct);
        }
        catch (JsonException error)
        {
            throw new HttpRequestException("La confirmación de la visita no es válida.", error);
        }

        if (confirmado is null || confirmado.EnvioId != intento.EnvioId || confirmado.IdOperacion != intento.IdOperacion)
            throw new HttpRequestException("El servidor no confirmó el identificador de la visita.");
        return new(confirmado.Repetido ? ResultadoAccion.YaProcesada : ResultadoAccion.Aceptada);
    }

    private static async Task<ProblemaIntentoDto?> LeerProblemaAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        if (respuesta.Content.Headers.ContentType?.MediaType != "application/problem+json") return null;
        try
        {
            return await respuesta.Content.ReadFromJsonAsync(JsonRepartidor.Default.ProblemaIntentoDto, ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void VerificarVigencia(SesionRepartidor sesion)
    {
        // Con el token vencido no tiene sentido llamar al servidor.
        if (sesion.Vencida(reloj.GetUtcNow())) throw new SesionExpiradaException();
    }

    private async Task<PerfilRepartidor> ObtenerPerfilAsync(string token, CancellationToken ct)
    {
        using var respuesta = await ConsultarAsync(RutaSesion, token, ct);
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync(JsonRepartidor.Default.PerfilRepartidor, ct)
               ?? throw new HttpRequestException("El servidor no devolvió el perfil.");
    }

    private async Task<HttpResponseMessage> ConsultarAsync(string ruta, string token, CancellationToken ct)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, ruta);
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var respuesta = await http.SendAsync(pedido, ct);
        if (respuesta.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)) return respuesta;
        respuesta.Dispose();
        throw new SesionExpiradaException();
    }
}
