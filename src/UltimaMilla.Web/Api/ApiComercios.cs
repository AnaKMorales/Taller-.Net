using System.Security.Claims;
using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Envios.Importacion;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Usuarios;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Api;

public sealed record LoginComercio(string Email, string Contrasena);
public sealed record OpcionesComercio(IReadOnlyList<ZonaAltaDto> Zonas, IReadOnlyList<PlazoAltaDto> Plazos);
public sealed record AltaComercio(string Referencia, SolicitudAltaPortal Solicitud, string ClaveEntrega);
public sealed record ResultadoAltaComercio(string Referencia, bool Repetido, AltaCreada Envio);

public static class ApiComercios
{
    // El límite del login de comercios es el común de los logins (Seguridad/ProteccionLogin.cs).
    public static IServiceCollection AgregarApiComercios(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapearApiComercios(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/comercios/sesion", async (LoginComercio solicitud, HttpContext context, IBuscadorUsuariosLogin usuarios,
            IntentosLogin intentos, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var usuario = await usuarios.ValidarAsync(intentos, context, solicitud.Email, solicitud.Contrasena, ct);
            if (usuario is not { Rol: RolUsuario.Comercio, CuentaComercialId: Guid cuenta }) return Results.Unauthorized();
            var identidad = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, usuario.UsuarioId.ToString()),
                new Claim(ClaimTypes.Name, usuario.Email),
                new Claim(ClaimTypes.Role, nameof(RolUsuario.Comercio)),
                new Claim(ClaimsUltimaMilla.Operador, usuario.OperadorId.ToString()),
                new Claim(ClaimsUltimaMilla.CuentaComercial, cuenta.ToString())], ApiRepartidor.Esquema);
            return Results.SignIn(new ClaimsPrincipal(identidad), authenticationScheme: ApiRepartidor.Esquema);
        }).AllowAnonymous().RequireRateLimiting(ProteccionLogin.PoliticaTasa).WithMetadata(new ApiOperativaMetadata());

        var api = app.MapGroup("/api/comercios")
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.AddAuthenticationSchemes(ApiRepartidor.Esquema)
                .RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Comercio)))
            .AddEndpointFilter<ErroresApiFilter>();
        api.MapGet("/envios/opciones", async (AltaEnvioPortalHandler alta, CancellationToken ct) =>
        {
            var opciones = await alta.OpcionesAsync(ct)
                ?? throw new InvalidOperationException("El operador no tiene reglas de entrega vigentes.");
            return Results.Ok(new OpcionesComercio(opciones.Zonas, opciones.Plazos));
        });
        api.MapPost("/envios/cotizacion", async (SolicitudAltaPortal solicitud, AltaEnvioPortalHandler alta, CancellationToken ct) =>
            Results.Ok(await alta.CotizarAsync(solicitud, ct)));
        api.MapPost("/envios", async (AltaComercio solicitud, ImportarEnviosHandler alta, CancellationToken ct) =>
        {
            var resultado = (await alta.Handle([new(solicitud.Referencia, solicitud.Solicitud, solicitud.ClaveEntrega)], ct)).Single();
            return resultado.Estado switch
            {
                EstadoFilaImportacion.Creado => Results.Json(new ResultadoAltaComercio(resultado.Referencia, false, resultado.Envio!), statusCode: 201),
                EstadoFilaImportacion.Existente => Results.Ok(new ResultadoAltaComercio(resultado.Referencia, true, resultado.Envio!)),
                EstadoFilaImportacion.Conflicto => Results.Problem(statusCode: 409, title: "La referencia ya existe con otros datos."),
                _ => Results.Problem(statusCode: 400, title: "Alta inválida.", detail: resultado.Error)
            };
        });
        return app;
    }
}
