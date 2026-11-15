using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Application.Configuracion;
using UltimaMilla.Application.Deposito;
using UltimaMilla.Application.Envios.Extravio;
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

        var incidencias = app.MapGroup("/api/incidencias/envios")
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Administrador), nameof(RolUsuario.Despachador)))
            .AddEndpointFilter<ErroresApiFilter>().AddEndpointFilter<AntiforgeryApiFilter>();
        incidencias.MapGet("/{envioId:guid}/extravio", async (Guid envioId, RegistrarExtravioHandler gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ConsultarAsync(envioId, ct)));
        incidencias.MapPost("/{envioId:guid}/extravio", async (Guid envioId, RegistrarExtravioCommand solicitud, RegistrarExtravioHandler gestion, CancellationToken ct) =>
            Results.Ok(await gestion.Handle(envioId, solicitud, ct)));

        var rutas = app.MapGroup("/api/repartidor/rutas")
            .RequireCors(ApiRepartidor.Cors)
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Repartidor)))
            .AddEndpointFilter<ErroresApiFilter>();
        rutas.MapGet("/", async (IHojasRuta hojas, CancellationToken ct) => Results.Ok(await hojas.ListarAsync(ct)));
        rutas.MapGet("/{id:guid}", async (Guid id, IHojasRuta hojas, CancellationToken ct) =>
            await hojas.ObtenerAsync(id, ct) is { } hoja ? Results.Ok(hoja) : Results.NotFound());

        var despacho = app.MapGroup("/api/despacho")
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser()
                .RequireRole(nameof(RolUsuario.Administrador), nameof(RolUsuario.Despachador)))
            .AddEndpointFilter<ErroresApiFilter>()
            .AddEndpointFilter<AntiforgeryApiFilter>();
        despacho.MapGet("/envios/{envioId:guid}/pendientes", async (Guid envioId, IDespachoPendientes gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ConsultarAsync(envioId, ct)));
        despacho.MapGet("/envios/disponibles", async (AsignarEnvioRutaHandler gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ListarEnviosAsync(ct)));
        despacho.MapGet("/rutas/planificables", async (AsignarEnvioRutaHandler gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ListarRutasAsync(ct)));
        despacho.MapGet("/recursos", async (AsignarEnvioRutaHandler gestion, CancellationToken ct) =>
            Results.Ok(await gestion.RecursosAsync(ct)));
        despacho.MapPost("/rutas/{rutaId:guid}/envios", async (Guid rutaId, AsignarEnvioRutaCommand solicitud,
            AsignarEnvioRutaHandler gestion, CancellationToken ct) =>
            Results.Json(await gestion.Handle(rutaId, solicitud, ct), statusCode: StatusCodes.Status201Created));
        despacho.MapPost("/rutas", async (CrearRutaPendientes solicitud, IDespachoPendientes gestion, CancellationToken ct) =>
            Results.Json(await gestion.CrearRutaAsync(solicitud, ct), statusCode: StatusCodes.Status201Created));
        despacho.MapGet("/rutas/{rutaId:guid}/preparacion", async (Guid rutaId, DespacharRutaHandler gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ConsultarAsync(rutaId, ct)));
        despacho.MapPost("/rutas/{rutaId:guid}/despachar", async (Guid rutaId, DespacharRutaCommand solicitud,
            DespacharRutaHandler gestion, CancellationToken ct) => Results.Ok(await gestion.Handle(rutaId, solicitud, ct)));
        despacho.MapPost("/rutas/{rutaId:guid}/pendientes", async (Guid rutaId, AsignarPendientes solicitud,
            IDespachoPendientes gestion, CancellationToken ct) =>
            Results.Json(await gestion.AsignarAsync(rutaId, solicitud, ct), statusCode: StatusCodes.Status201Created));
        despacho.MapPost("/envios/{envioId:guid}/devolucion-pendientes", async (Guid envioId, DevolverPendientes solicitud,
            IDespachoPendientes gestion, CancellationToken ct) =>
            Results.Json(await gestion.DevolverAsync(envioId, solicitud.VersionEsperadaEnvio, ct), statusCode: StatusCodes.Status201Created));

        var carga = app.MapGroup("/api/carga/rutas")
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Administrador),
                nameof(RolUsuario.Despachador), nameof(RolUsuario.OperarioDeposito)))
            .AddEndpointFilter<ErroresApiFilter>().AddEndpointFilter<AntiforgeryApiFilter>();
        MapearCarga(carga);
        var cargaRepartidor = app.MapGroup("/api/repartidor/carga/rutas")
            .WithMetadata(new ApiOperativaMetadata()).RequireCors(ApiRepartidor.Cors)
            .RequireAuthorization(Politicas.Repartidor).AddEndpointFilter<ErroresApiFilter>();
        MapearCarga(cargaRepartidor);

        var recepcion = app.MapGroup("/api/deposito/envios")
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Administrador),
                nameof(RolUsuario.Despachador), nameof(RolUsuario.OperarioDeposito)))
            .AddEndpointFilter<ErroresApiFilter>().AddEndpointFilter<AntiforgeryApiFilter>();
        recepcion.MapGet("/{envioId:guid}/recepcion", async (Guid envioId, RecepcionDepositoHandler gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ConsultarAsync(envioId, ct)));
        recepcion.MapPost("/{envioId:guid}/recepcion/bultos", async (Guid envioId, EscanearRecepcionCommand solicitud,
            RecepcionDepositoHandler gestion, CancellationToken ct) => Results.Ok(await gestion.EscanearAsync(envioId, solicitud, ct)));
        recepcion.MapPost("/{envioId:guid}/recepcion/discrepancias", async (Guid envioId, RegistrarDiscrepanciaCommand solicitud,
            RecepcionDepositoHandler gestion, CancellationToken ct) => Results.Ok(await gestion.RegistrarDiscrepanciaAsync(envioId, solicitud, ct)));
        recepcion.MapPost("/{envioId:guid}/recepcion/confirmar", async (Guid envioId, ConfirmarRecepcionCommand solicitud,
            RecepcionDepositoHandler gestion, CancellationToken ct) => Results.Ok(await gestion.ConfirmarAsync(envioId, solicitud, ct)));
        recepcion.MapGet("/{envioId:guid}/devolucion", async (Guid envioId, DevolucionDepositoHandler gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ConsultarAsync(envioId, ct)));
        recepcion.MapPost("/{envioId:guid}/devolucion/iniciar", async (Guid envioId, VersionDevolucionCommand solicitud,
            DevolucionDepositoHandler gestion, CancellationToken ct) =>
            Results.Json(await gestion.IniciarAsync(envioId, solicitud, ct), statusCode: StatusCodes.Status201Created));
        recepcion.MapPost("/{envioId:guid}/devolucion/bultos", async (Guid envioId, EscanearDevolucionCommand solicitud,
            DevolucionDepositoHandler gestion, CancellationToken ct) => Results.Ok(await gestion.EscanearAsync(envioId, solicitud, ct)));
        recepcion.MapPost("/{envioId:guid}/devolucion/completar", async (Guid envioId, VersionDevolucionCommand solicitud,
            DevolucionDepositoHandler gestion, CancellationToken ct) => Results.Ok(await gestion.CompletarAsync(envioId, solicitud, ct)));

        var cierre = app.MapGroup("/api/deposito")
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Administrador),
                nameof(RolUsuario.Despachador), nameof(RolUsuario.OperarioDeposito)))
            .AddEndpointFilter<ErroresApiFilter>().AddEndpointFilter<AntiforgeryApiFilter>();
        cierre.MapGet("/devoluciones/abiertas", async (DevolucionDepositoHandler gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ListarAbiertasAsync(ct)));
        cierre.MapGet("/rutas/por-rendir", async (RendirRutaHandler gestion, CancellationToken ct) => Results.Ok(await gestion.ListarAsync(ct)));
        cierre.MapGet("/rutas/{rutaId:guid}/rendicion", async (Guid rutaId, RendirRutaHandler gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ConsultarAsync(rutaId, ct)));
        cierre.MapPost("/rutas/{rutaId:guid}/rendir", async (Guid rutaId, RendirRutaCommand solicitud,
            RendirRutaHandler gestion, CancellationToken ct) => Results.Ok(await gestion.Handle(rutaId, solicitud, ct)));

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
        var flota = app.MapGroup("/api/administracion/flota")
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Administrador)))
            .AddEndpointFilter<ErroresApiFilter>().AddEndpointFilter<AntiforgeryApiFilter>();
        flota.MapGet("/repartidores", async (IAdministracionFlota gestion, CancellationToken ct) => Results.Ok(await gestion.RepartidoresAsync(ct)));
        flota.MapGet("/vehiculos", async (IAdministracionFlota gestion, CancellationToken ct) => Results.Ok(await gestion.VehiculosAsync(ct)));
        flota.MapPost("/repartidores", async (CrearRepartidor solicitud, IAdministracionFlota gestion, CancellationToken ct) =>
            Results.Json(await gestion.CrearRepartidorAsync(solicitud, ct), statusCode: StatusCodes.Status201Created));
        flota.MapPost("/vehiculos", async (CrearVehiculo solicitud, IAdministracionFlota gestion, CancellationToken ct) =>
            Results.Json(await gestion.CrearVehiculoAsync(solicitud, ct), statusCode: StatusCodes.Status201Created));
        var comercios = app.MapGroup("/api/administracion/comercios")
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Administrador)))
            .AddEndpointFilter<ErroresApiFilter>().AddEndpointFilter<AntiforgeryApiFilter>();
        comercios.MapGet("/", async (IAdministracionComercios gestion, CancellationToken ct) => Results.Ok(await gestion.CuentasAsync(ct)));
        comercios.MapPost("/", async (CrearComercio solicitud, IAdministracionComercios gestion, CancellationToken ct) =>
            Results.Json(await gestion.CrearAsync(solicitud, ct), statusCode: StatusCodes.Status201Created));
        comercios.MapGet("/{cuentaId:guid}/usuarios", async (Guid cuentaId, IAdministracionComercios gestion, CancellationToken ct) =>
            Results.Ok(await gestion.UsuariosAsync(cuentaId, ct)));
        comercios.MapPut("/{cuentaId:guid}", async (Guid cuentaId, ModificarCuentaComercial solicitud,
            IAdministracionComercios gestion, CancellationToken ct) => Results.Ok(await gestion.ModificarCuentaAsync(cuentaId, solicitud, ct)));
        comercios.MapPost("/{cuentaId:guid}/baja", async (Guid cuentaId, BajaCuentaComercial solicitud,
            IAdministracionComercios gestion, CancellationToken ct) => Results.Ok(await gestion.DarBajaCuentaAsync(cuentaId, solicitud, ct)));
        comercios.MapPost("/{cuentaId:guid}/usuarios", async (Guid cuentaId, CrearUsuarioComercio solicitud,
            IAdministracionComercios gestion, CancellationToken ct) =>
            Results.Json(await gestion.CrearUsuarioAsync(cuentaId, solicitud, ct), statusCode: StatusCodes.Status201Created));
        comercios.MapPut("/{cuentaId:guid}/usuarios/{usuarioId:guid}", async (Guid cuentaId, Guid usuarioId,
            ModificarUsuarioComercio solicitud, IAdministracionComercios gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ModificarUsuarioAsync(cuentaId, usuarioId, solicitud, ct)));
        comercios.MapPost("/{cuentaId:guid}/usuarios/{usuarioId:guid}/baja", async (Guid cuentaId, Guid usuarioId,
            EstadoUsuarioEsperado solicitud, IAdministracionComercios gestion, CancellationToken ct) =>
            Results.Ok(await gestion.DarBajaUsuarioAsync(cuentaId, usuarioId, solicitud, ct)));
        var zonas = app.MapGroup("/api/administracion/zonas")
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Administrador)))
            .AddEndpointFilter<ErroresApiFilter>().AddEndpointFilter<AntiforgeryApiFilter>();
        zonas.MapGet("/", async (IAdministracionZonas gestion, CancellationToken ct) => Results.Ok(await gestion.ZonasAsync(ct)));
        zonas.MapPost("/", async (CrearZonaCobertura solicitud, IAdministracionZonas gestion, CancellationToken ct) =>
            Results.Json(await gestion.CrearZonaAsync(solicitud, ct), statusCode: StatusCodes.Status201Created));
        zonas.MapGet("/{zonaId:guid}/franjas", async (Guid zonaId, IAdministracionZonas gestion, CancellationToken ct) =>
            Results.Ok(await gestion.FranjasAsync(zonaId, ct)));
        zonas.MapPost("/{zonaId:guid}/franjas", async (Guid zonaId, CrearFranjaHoraria solicitud,
            IAdministracionZonas gestion, CancellationToken ct) =>
            Results.Json(await gestion.CrearFranjaAsync(zonaId, solicitud, ct), statusCode: StatusCodes.Status201Created));
        var identidad = app.MapGroup("/api/administracion/identidad")
            .WithMetadata(new ApiOperativaMetadata())
            .RequireAuthorization(p => p.RequireAuthenticatedUser().RequireRole(nameof(RolUsuario.Administrador)))
            .AddEndpointFilter<ErroresApiFilter>().AddEndpointFilter<AntiforgeryApiFilter>();
        identidad.MapGet("/", async (IAdministracionIdentidadOperador gestion, CancellationToken ct) => Results.Ok(await gestion.ConsultarAsync(ct)));
        identidad.MapPut("/", async (ModificarIdentidadOperador solicitud, IAdministracionIdentidadOperador gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ModificarAsync(solicitud, ct)));
        app.MapGet("/api/operador/identidad", async (IConsultaIdentidadOperador consulta, CancellationToken ct) => Results.Ok(await consulta.ActualAsync(ct)))
            .WithMetadata(new ApiOperativaMetadata()).RequireAuthorization().AddEndpointFilter<ErroresApiFilter>();
        app.MapGet("/api/seguimiento/{token}/identidad", async (string token, HttpContext contexto, IConsultaIdentidadOperador consulta, CancellationToken ct) =>
        {
            contexto.Response.Headers.CacheControl = "no-store";
            return await consulta.PorSeguimientoAsync(token, ct) is { } datos ? Results.Ok(datos) : Results.NotFound();
        }).AllowAnonymous().RequireRateLimiting(SeguimientoSeguro.PoliticaTasa);
        return app;
    }

    private static void MapearCarga(RouteGroupBuilder grupo)
    {
        grupo.MapGet("/{rutaId:guid}", async (Guid rutaId, RegistrarCargaRutaHandler gestion, CancellationToken ct) =>
            Results.Ok(await gestion.ConsultarAsync(rutaId, ct)));
        grupo.MapPost("/{rutaId:guid}/bultos", async (Guid rutaId, RegistrarCargaRutaCommand solicitud,
            RegistrarCargaRutaHandler gestion, CancellationToken ct) => Results.Ok(await gestion.Handle(rutaId, solicitud, ct)));
    }
}

internal sealed class ApiOperativaMetadata;
internal sealed record DevolverPendientes(Guid VersionEsperadaEnvio);

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
            logger.LogError(error, "Falló la API operativa. TraceId {TraceId}",
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
            return Results.Problem(statusCode: 500, title: "No se pudo completar la operación.");
        }
    }
}
