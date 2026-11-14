using System.Security.Claims;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Deposito;
using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Envios.ReprogramacionPublica;
using UltimaMilla.Application.Portal;
using UltimaMilla.Application.Tablero;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Cache;
using UltimaMilla.Infrastructure.Mensajeria;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Backoffice;
using UltimaMilla.Infrastructure.Persistence.Envios;
using UltimaMilla.Infrastructure.Persistence.Configuracion;
using UltimaMilla.Infrastructure.Persistence.Despacho;
using UltimaMilla.Infrastructure.Persistence.Deposito;
using UltimaMilla.Infrastructure.Persistence.Intentos;
using UltimaMilla.Infrastructure.Persistence.Tablero;
using UltimaMilla.Web.Components;
using UltimaMilla.Web.Api;
using UltimaMilla.Web.Desarrollo;
using UltimaMilla.Web.Observabilidad;
using UltimaMilla.Web.Seguridad;
using UltimaMilla.Web.Tiempo;

var builder = WebApplication.CreateBuilder(args);
builder.AgregarTelemetriaWeb();

builder.Services.AddSerilog((services, configuration) =>
{
    var ajustes = services.GetRequiredService<IConfiguration>();
    configuration.ReadFrom.Configuration(ajustes)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "UltimaMilla.Web")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console(new JsonFormatter(renderMessage: true));
    if (ajustes.GetValue<bool>("OpenTelemetry:ExportarOtlp"))
        configuration.WriteTo.OpenTelemetry(options =>
        {
            options.ResourceAttributes = new Dictionary<string, object>
            {
                ["service.name"] = "UltimaMilla.Web",
                ["deployment.environment.name"] = builder.Environment.EnvironmentName
            };
            options.OnBeginSuppressInstrumentation = OpenTelemetry.SuppressInstrumentationScope.Begin;
        });
}, preserveStaticLogger: true);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"], timeout: TimeSpan.FromSeconds(3));

// Detrás de Traefik: esquema/host/IP reales solo desde la red confiable (ForwardedHeaders:RedConfiable).
builder.Services.AgregarCabecerasReenviadas();

// Claves de Data Protection (cookies, antiforgery): en una ruta persistente para que sobrevivan a reinicios.
var rutaClaves = builder.Configuration["DataProtection:RutaClaves"];
if (string.IsNullOrWhiteSpace(rutaClaves) && builder.Environment.IsDevelopment())
    rutaClaves = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UltimaMilla", "claves-dp");
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("UltimaMilla");
if (!string.IsNullOrWhiteSpace(rutaClaves))
    dataProtection.PersistKeysToFileSystem(Directory.CreateDirectory(rutaClaves));
// Enums como texto en la API.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Razor Pages para el Backoffice y Blazor para el Portal
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(o => o.HeaderName = ApiOperativa.CabeceraAntiforgery);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Sesión con cookie para backoffice y portal; token Bearer para la PWA del repartidor.
// El selector usa el token solo si el pedido trae "Authorization: Bearer"; si no, la cookie de siempre.
builder.Services.AddAuthentication(Politicas.EsquemaSelector)
    .AddPolicyScheme(Politicas.EsquemaSelector, Politicas.EsquemaSelector, o => o.ForwardDefaultSelector = context =>
        context.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? ApiRepartidor.Esquema
            : CookieAuthenticationDefaults.AuthenticationScheme)
    // Token protegido con Data Protection: no hay clave de firma que guardar en el repo.
    .AddBearerToken(ApiRepartidor.Esquema, o => o.BearerTokenExpiration = TimeSpan.FromHours(8))
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/acceso-denegado";
        o.Cookie.Name = "ultimamilla.sesion";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        ApiOperativa.ConfigurarRespuestasDeAutenticacion(o);
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.Backoffice, p => p.RequireRole(Politicas.RolesBackoffice))
    .AddPolicy(Politicas.Portal, p => p.RequireRole(nameof(UltimaMilla.Domain.Usuarios.RolUsuario.Comercio)))
    .AddPolicy(Politicas.Administracion, p => p.RequireRole(nameof(UltimaMilla.Domain.Usuarios.RolUsuario.Administrador)))
    .AddPolicy(Politicas.Repartidor, p => p.AddAuthenticationSchemes(ApiRepartidor.Esquema)
        .RequireRole(nameof(UltimaMilla.Domain.Usuarios.RolUsuario.Repartidor)));

// La PWA se sirve desde otro origen; solo los configurados en Repartidor:Origenes pueden llamar a su API.
builder.Services.AddCors(o => o.AddPolicy(ApiRepartidor.Cors, p => p
    .WithOrigins(builder.Configuration.GetSection("Repartidor:Origenes").Get<string[]>() ?? [])
    .WithHeaders("Authorization", "Content-Type", "traceparent")
    .WithMethods("GET", "POST")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantActual, TenantActualHttp>();

// Tablero en tiempo real (SignalR).
builder.Services.AgregarBackofficeUltimaMilla();
builder.Services.AddSingleton<INotificadorTablero, NotificadorTableroSignalR>();
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<AppDbContext>((sp, options) => options
    .UseNpgsql(builder.Configuration.GetConnectionString("UltimaMilla"))
    .AddInterceptors(sp.GetRequiredService<PublicarCambiosInterceptor>(),
        sp.GetRequiredService<InvalidarSeguimientoInterceptor>()));

// Cola de fallidos (avisos.dlq) para /backoffice/fallidos. Clave: RabbitMq__Clave (variable de entorno).
builder.Services.AgregarColaFallidos(builder.Configuration);
// Reenvío de avisos fallidos desde el portal del comercio: publica directo en la cola (no cambia la base).
builder.Services.AgregarPublicacionDirecta(builder.Configuration);
builder.Services.AddScoped<ReenviarAvisoHandler>();

builder.Services.AddScoped<CrearEnvioHandler>();
builder.Services.AddScoped<IAltaEnvioRepository, AltaEnvioRepository>();
// Alta de envíos desde el portal: opciones del comercio, tarifa con el tarifario vigente y fechas en el servidor.
builder.Services.AddSingleton<ICalculadorTarifa, CalculadorTarifa>();
builder.Services.AddScoped<IOpcionesAlta, OpcionesAlta>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Operacion").Get<OpcionesOperacion>() ?? new OpcionesOperacion());
builder.Services.AddScoped<AltaEnvioPortalHandler>();
builder.Services.AddScoped<UltimaMilla.Application.Envios.Importacion.IImportacionEnvios, ImportacionEnvios>();
builder.Services.AddScoped<UltimaMilla.Application.Envios.Importacion.ImportarEnviosHandler>();
// Reprogramación por el destinatario desde el seguimiento público (requisito 26).
builder.Services.AddScoped<IReprogramacionPublicaRepository, ReprogramacionPublicaRepository>();
builder.Services.AddScoped<ReprogramarPorDestinatarioHandler>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EstadoSincronizaciones>();
builder.Services.AgregarHojasRuta();
builder.Services.AddScoped<IDespachoPendientes, DespachoPendientes>();
builder.Services.AddScoped<IDespachoRutaRepository, DespachoRutaRepository>();
builder.Services.AddScoped<DespacharRutaHandler>();
builder.Services.AddScoped<IAsignacionEnvioRutaRepository, AsignacionEnvioRutaRepository>();
builder.Services.AddScoped<AsignarEnvioRutaHandler>();
builder.Services.AddScoped<IRegistroCargaRutaRepository, DespachoRutaRepository>();
builder.Services.AddScoped<RegistrarCargaRutaHandler>();
builder.Services.AddScoped<IRecepcionDepositoRepository, RecepcionDepositoRepository>();
builder.Services.AddScoped<RecepcionDepositoHandler>();
builder.Services.AddScoped<IDevolucionDepositoRepository, CierreDepositoRepository>();
builder.Services.AddScoped<IRendicionRutaRepository, CierreDepositoRepository>();
builder.Services.AddScoped<DevolucionDepositoHandler>();
builder.Services.AddScoped<RendirRutaHandler>();
builder.Services.AgregarRegistroIntentos();
builder.Services.AgregarConfiguracionVersionada();

// Caché del tarifario y del seguimiento (ADR-004).
builder.Services.AgregarCacheUltimaMilla(builder.Configuration.GetConnectionString("Redis"),
    builder.Configuration.GetSection("Cache").Get<CacheOpciones>());

// Seguimiento público: limitación de tasa por IP contra la enumeración de envíos.
builder.Services.AgregarLimitesSeguimiento(builder.Configuration);

var app = builder.Build();
app.Services.GetRequiredService<EstadoSincronizaciones>().Iniciar();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// HTTPS lo termina Traefik en AWS.
app.UseSerilogRequestLogging(options =>
{
    options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
    options.EnrichDiagnosticContext = (diagnostic, context) =>
        diagnostic.Set("TraceId", Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier);
    // La URL real del seguimiento contiene el token público. Se registra la plantilla de ruta.
    options.GetMessageTemplateProperties = (context, _, elapsed, statusCode) =>
    [
        new LogEventProperty("RequestMethod", new ScalarValue(context.Request.Method)),
        new LogEventProperty("RequestPath", new ScalarValue(
            (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "[sin ruta]")),
        new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
        new LogEventProperty("Elapsed", new ScalarValue(elapsed))
    ];
});
if (string.IsNullOrWhiteSpace(rutaClaves))
    app.Logger.LogWarning("DataProtection:RutaClaves no está configurada: las claves de las cookies se pierden al reiniciar.");

// HTTPS lo termina Traefik en AWS: primero se toma el esquema/IP reales de X-Forwarded-*.
app.UseForwardedHeaders();
app.UsarCabecerasSeguras();
app.UseCors();
app.UseAuthentication();

// Resuelve el operador de la sesión en cada petición.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true && !Path.HasExtension(context.Request.Path.Value))
    {
        try
        {
            var resolver = context.RequestServices.GetRequiredService<ITenantResolver>();
            context.Items[TenantActualHttp.Clave] = await resolver.DesdeClaimsAsync(context.User, context.RequestAborted);
        }
        catch (AislamientoOperadorException)
        {
            // Usuario desactivado: se cierra la sesión.
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }
    }
    await next();
});

app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

// PWA del repartidor: la publicación se copia a wwwroot/repartidor (Dockerfile de Web), fuera del manifiesto
// de MapStaticAssets, por eso se sirve con UseStaticFiles (solo bajo /repartidor, para no tapar a MapStaticAssets).
// Las rutas internas de la PWA caen en su index.html.
app.UseWhen(c => c.Request.Path.StartsWithSegments("/repartidor"), pwa => pwa.UseStaticFiles(StaticFilesPwa.Opciones()));
app.MapStaticAssets();
app.MapFallbackToFile("/repartidor/{*ruta:nonfile}", "repartidor/index.html");
app.MapRazorPages();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapHub<TableroHub>(TableroHub.Ruta);
app.MapearApiOperativa();
app.MapApiRepartidor();
app.MapHealthChecks("/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();

// Seguimiento público (sin login). La página del destinatario es /seguimiento/{token} (Pages/Seguimiento.razor).
app.MapGet("/api/seguimiento/{token}", async (string token, ISeguimientoPublico seguimiento, CancellationToken ct) =>
        await seguimiento.ConsultarAsync(token, ct) is { } envio ? Results.Ok(envio) : Results.NotFound())
    .RequireRateLimiting(SeguimientoSeguro.PoliticaTasa);

if (app.Environment.IsDevelopment())
{
    // Endpoints de prueba (solo desarrollo).
    var dev = app.MapGroup("/dev");

    // Dos operadores con configuración distinta (EscenarioDemo); /datos-demo-pwa conserva el operador único para la PWA.
    // Cuerpo opcional {"contrasena": "..."}: sin él se usa la clave por defecto (solo local); en un servidor público
    // sembrar-demo.sh manda una clave aleatoria que no se devuelve en la respuesta.
    dev.MapPost("/datos-demo", async (DbContextOptions<AppDbContext> options, SolicitudDatosDemo? solicitud, CancellationToken ct) =>
        solicitud?.Contrasena is { Length: < SolicitudDatosDemo.LargoMinimo }
            ? Results.BadRequest(new { error = $"La contraseña debe tener al menos {SolicitudDatosDemo.LargoMinimo} caracteres." })
            : Results.Ok(await EscenarioDemo.CrearAsync(options, solicitud?.Contrasena, ct)));

    dev.MapPost("/datos-demo-pwa", async (DbContextOptions<AppDbContext> options, CancellationToken ct) =>
        Results.Ok(await DatosDemo.CrearAsync(options, ct, incluirVisitaParcial: true)));

    dev.MapGet("/tarifario/{operadorId:guid}", async (Guid operadorId, ITarifarioVigente tarifario, CancellationToken ct) =>
        await tarifario.ObtenerAsync(operadorId, ct) is { } cuadro ? Results.Ok(cuadro) : Results.NotFound());

    dev.MapPost("/tarifario/{operadorId:guid}/invalidar",
        async (Guid operadorId, IInvalidacionTarifario invalidacion, CancellationToken ct) =>
        {
            await invalidacion.InvalidarAsync(operadorId, ct);
            return Results.NoContent();
        });

    dev.MapGet("/cache/metricas", (CacheMetricas metricas) => metricas.Resumen());

    var simular = dev.MapGroup("/simular").RequireAuthorization(Politicas.Backoffice);

    simular.MapPost("/alta", async (ITenantActual tenant, DbContextOptions<AppDbContext> options, CancellationToken ct) =>
    {
        if (tenant.Contexto is not { } contexto) return Results.Forbid();
        await DatosDemo.SimularAltaAsync(options, contexto.OperadorId, ct);
        return Results.NoContent();
    });

    // Suscribe la cuenta demo a los destinos del receptor de prueba (ok / flaky / fail) para demostrar webhooks.
    // El secreto debe coincidir con Receptor__Secreto del receptor (por defecto, el de deploy/compose.yaml).
    simular.MapPost("/webhooks", async (ITenantActual tenant, DbContextOptions<AppDbContext> options,
        string? receptor, string? secreto, string? eventos, CancellationToken ct) =>
    {
        if (tenant.Contexto is not { } contexto) return Results.Forbid();
        if (!Uri.TryCreate(receptor ?? "http://receptor:8080", UriKind.Absolute, out var destino) ||
            destino.Scheme is not ("http" or "https"))
            return Results.BadRequest("receptor debe ser una URL http(s) absoluta.");
        // Por defecto solo "Admitir" (el evento del alta): así la demostración no se mezcla con eventos anteriores.
        var tipos = (eventos ?? nameof(TipoEvento.Admitir)).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => Enum.TryParse<TipoEvento>(t, out var tipo) && Enum.IsDefined(tipo) ? (TipoEvento?)tipo : null).ToList();
        if (tipos.Count == 0 || tipos.Any(t => t is null))
            return Results.BadRequest("eventos debe ser una lista separada por comas de tipos de evento válidos.");
        return Results.Ok(await DatosDemo.SuscribirReceptorAsync(options, contexto.OperadorId, destino,
            string.IsNullOrWhiteSpace(secreto) ? "secreto-demo" : secreto, tipos.Select(t => t!.Value).ToList(), ct));
    });

    simular.MapPost("/recepcion", async (ITenantActual tenant, DbContextOptions<AppDbContext> options, CancellationToken ct) =>
    {
        if (tenant.Contexto is not { } contexto) return Results.Forbid();
        return await DatosDemo.SimularRecepcionAsync(options, contexto.OperadorId, ct)
            ? Results.NoContent()
            : Results.NotFound();
    });
}

app.Run();
