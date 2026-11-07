using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using UltimaMilla.Application.Envios.RegistrarIntento;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Usuarios;
using UltimaMilla.Web.Api;

namespace UltimaMilla.Web.Seguridad;

public sealed record SolicitudLoginRepartidor(string Email, string Contrasena);

public sealed record PerfilRepartidor(Guid UsuarioId, string Email);

// API de la PWA del repartidor (ADR-005). La PWA corre en otro origen y no puede usar la cookie
// SameSite=Strict del backoffice: recibe un token Bearer que envía en el encabezado Authorization.
public static class ApiRepartidor
{
    public const string Esquema = BearerTokenDefaults.AuthenticationScheme;
    public const string Cors = "PwaRepartidor";

    public static void MapApiRepartidor(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/repartidor").RequireCors(Cors);

        // Mismo validador que el login de Web: email normalizado, usuario activo y hash PBKDF2.
        api.MapPost("/sesion", async (SolicitudLoginRepartidor solicitud, IBuscadorUsuariosLogin usuarios, CancellationToken ct) =>
        {
            var usuario = await usuarios.ValidarAsync(solicitud.Email ?? "", solicitud.Contrasena ?? "", ct);
            // Credenciales inválidas y rol distinto dan la misma respuesta: no se revela qué cuentas existen.
            if (usuario is null || usuario.Rol != RolUsuario.Repartidor) return Results.Unauthorized();

            // Mismos claims que espera TenantResolver. El operador sale de la base, nunca del cliente.
            var identidad = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, usuario.UsuarioId.ToString()),
                new Claim(ClaimTypes.Name, usuario.Email),
                new Claim(ClaimTypes.Role, nameof(RolUsuario.Repartidor)),
                new Claim(ClaimsUltimaMilla.Operador, usuario.OperadorId.ToString())
            ], Esquema);

            // El manejador Bearer responde { tokenType, accessToken, expiresIn, refreshToken }.
            return Results.SignIn(new ClaimsPrincipal(identidad), authenticationScheme: Esquema);
        }).AllowAnonymous();

        // Confirma que el token sigue vigente y que el usuario continúa activo. TenantResolver lo verifica
        // contra la base en cada pedido (middleware de Program.cs). La política vuelve a leer el token, así que
        // se exige además el contexto de tenant: sin él (usuario desactivado o rol cambiado) => 401.
        // Todo endpoint futuro de esta API debe tomar operador y repartidor de ITenantActual, nunca del cuerpo.
        api.MapGet("/sesion", (ITenantActual tenant, ClaimsPrincipal usuario) =>
                tenant.Contexto is { UsuarioId: { } id }
                    ? Results.Ok(new PerfilRepartidor(id, usuario.Identity?.Name ?? ""))
                    : Results.Unauthorized())
            .RequireAuthorization(Politicas.Repartidor);

        api.MapPost("/intentos", async (SolicitudIntentoRepartidor solicitud, ITenantActual tenant,
            RegistrarIntentoHandler handler, CancellationToken ct) =>
        {
            if (tenant.Contexto is not { UsuarioId: not null }) return Results.Unauthorized();
            try
            {
                var resultado = await handler.Handle(solicitud.AComando(), ct);
                return Results.Ok(resultado);
            }
            catch (ConflictoIntentoException error)
            {
                return Results.Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "La versión ya no está vigente. Vuelve a consultar antes de guardar.",
                    extensions: new Dictionary<string, object?> { ["conflicto"] = error.Conflicto });
            }
        })
        .RequireAuthorization(Politicas.Repartidor)
        .AddEndpointFilter<ErroresApiFilter>()
        .Produces<IntentoRegistradoDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
