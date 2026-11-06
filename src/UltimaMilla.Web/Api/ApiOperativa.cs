using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Configuracion;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Domain.Usuarios;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Api;

public static class ApiOperativa
{
    public const string CabeceraAntiforgery = "X-CSRF-TOKEN";

    public static void ConfigurarRespuestasDeAutenticacion(CookieAuthenticationOptions options)
    {
        var loginOriginal = options.Events.OnRedirectToLogin;
        var denegadoOriginal = options.Events.OnRedirectToAccessDenied;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<ApiOperativaMetadata>() is null)
                return loginOriginal(context);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<ApiOperativaMetadata>() is null)
                return denegadoOriginal(context);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }

    public static IEndpointRouteBuilder MapearApiOperativa(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken, cabecera = CabeceraAntiforgery });
        }).RequireAuthorization().WithMetadata(new ApiOperativaMetadata());

        var rutas = app.MapGroup("/api/repartidor/rutas")
            .RequireCors(ApiRepartidor.Cors)
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Repartidor)))
            .AddEndpointFilter<ErroresApiFilter>();
        rutas.MapGet("/", async (IHojasRuta hojas, CancellationToken ct) => Results.Ok(await hojas.ListarAsync(ct)));
        rutas.MapGet("/{id:guid}", async (Guid id, IHojasRuta hojas, CancellationToken ct) =>
            await hojas.ObtenerAsync(id, ct) is { } hoja ? Results.Ok(hoja) : Results.NotFound());

        var configuracion = app.MapGroup("/api/configuracion")
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Administrador)))
            .AddEndpointFilter<ErroresApiFilter>()
            .AddEndpointFilter<AntiforgeryApiFilter>();
        configuracion.MapGet("/zonas", async (IConfiguracionVersionada gestion, CancellationToken ct) => Results.Ok(await gestion.ListarZonasAsync(ct)));
        configuracion.MapGet("/reglas", async (IConfiguracionVersionada gestion, CancellationToken ct) => Results.Ok(await gestion.ListarReglasAsync(ct)));
        configuracion.MapGet("/reglas/vigentes", async (IConfiguracionVersionada gestion, CancellationToken ct) =>
            await gestion.ReglasVigentesAsync(ct) is { } datos ? Results.Ok(datos) : Results.NotFound());
        configuracion.MapGet("/reglas/{id:guid}", async (Guid id, IConfiguracionVersionada gestion, CancellationToken ct) =>
            await gestion.ObtenerReglasAsync(id, ct) is { } datos ? Results.Ok(datos) : Results.NotFound());
        configuracion.MapGet("/tarifarios", async (IConfiguracionVersionada gestion, CancellationToken ct) => Results.Ok(await gestion.ListarTarifariosAsync(ct)));
        configuracion.MapGet("/tarifarios/vigente", async (IConfiguracionVersionada gestion, CancellationToken ct) =>
            await gestion.TarifarioVigenteAsync(ct) is { } datos ? Results.Ok(datos) : Results.NotFound());
        configuracion.MapGet("/tarifarios/{id:guid}", async (Guid id, IConfiguracionVersionada gestion, CancellationToken ct) =>
            await gestion.ObtenerTarifarioAsync(id, ct) is { } datos ? Results.Ok(datos) : Results.NotFound());
        configuracion.MapPost("/reglas", async (NuevaVersionReglas solicitud, IConfiguracionVersionada gestion, CancellationToken ct) =>
        {
            var creada = await gestion.CrearReglasAsync(solicitud, ct);
            return Results.Created($"/api/configuracion/reglas/{creada.Version.Id}", creada);
        });
        configuracion.MapPost("/tarifarios", async (NuevaVersionTarifario solicitud, IConfiguracionVersionada gestion, CancellationToken ct) =>
        {
            var creada = await gestion.CrearTarifarioAsync(solicitud, ct);
            return Results.Created($"/api/configuracion/tarifarios/{creada.Version.Id}", creada);
        });
        return app;
    }
}

internal sealed class ApiOperativaMetadata;

internal sealed class AntiforgeryApiFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
            catch (AntiforgeryValidationException)
            { return Results.Problem(statusCode: 400, title: "Falta un token antiforgery válido."); }
        }
        return await next(context);
    }
}

internal sealed class ErroresApiFilter(ILogger<ErroresApiFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try { return await next(context); }
        catch (AislamientoOperadorException) { return Results.Forbid(); }
        catch (ConflictoConcurrenciaException)
        { return Results.Problem(statusCode: 409, title: "La versión ya no está vigente. Vuelve a consultar antes de guardar."); }
        catch (ArgumentException error)
        { return Results.Problem(statusCode: 400, title: "Solicitud inválida.", detail: error.Message); }
        catch (InvalidOperationException error)
        { return Results.Problem(statusCode: 409, title: "Operación no permitida.", detail: error.Message); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Falló la API operativa. TraceId {TraceId}", context.HttpContext.TraceIdentifier);
            return Results.Problem(statusCode: 500, title: "No se pudo completar la operación.");
        }
    }
}
