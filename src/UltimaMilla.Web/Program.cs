using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Tablero;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Cache;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Backoffice;
using UltimaMilla.Infrastructure.Persistence.Tablero;
using UltimaMilla.Web.Components;
using UltimaMilla.Web.Desarrollo;
using UltimaMilla.Web.Seguridad;
using UltimaMilla.Web.Tiempo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
// Enums como texto en la API.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Razor Pages para el Backoffice y Blazor para el Portal
builder.Services.AddRazorPages();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Sesión con cookie.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/acceso-denegado";
        o.Cookie.Name = "ultimamilla.sesion";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.Backoffice, p => p.RequireRole(Politicas.RolesBackoffice))
    .AddPolicy(Politicas.Portal, p => p.RequireRole(nameof(UltimaMilla.Domain.Usuarios.RolUsuario.Comercio)))
    .AddPolicy(Politicas.Administracion, p => p.RequireRole(nameof(UltimaMilla.Domain.Usuarios.RolUsuario.Administrador)));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantActual, TenantActualHttp>();

// Tablero en tiempo real (SignalR).
builder.Services.AgregarBackofficeUltimaMilla();
builder.Services.AddSingleton<INotificadorTablero, NotificadorTableroSignalR>();
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<AppDbContext>((sp, options) => options
    .UseNpgsql(builder.Configuration.GetConnectionString("UltimaMilla"))
    .AddInterceptors(sp.GetRequiredService<PublicarCambiosInterceptor>()));

builder.Services.AddScoped<CrearEnvioHandler>();
builder.Services.AddScoped<IAltaEnvioRepository, AltaEnvioRepository>();
builder.Services.AddSingleton(TimeProvider.System);

// Caché del tarifario y del seguimiento (ADR-004).
builder.Services.AgregarCacheUltimaMilla(builder.Configuration.GetConnectionString("Redis"),
    builder.Configuration.GetSection("Cache").Get<CacheOpciones>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// HTTPS lo termina Traefik en AWS.
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
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapHub<TableroHub>(TableroHub.Ruta);

// Seguimiento público (sin login).
app.MapGet("/api/seguimiento/{token}", async (string token, ISeguimientoPublico seguimiento, CancellationToken ct) =>
    await seguimiento.ConsultarAsync(token, ct) is { } envio ? Results.Ok(envio) : Results.NotFound());

if (app.Environment.IsDevelopment())
{
    // Endpoints de prueba (solo desarrollo).
    var dev = app.MapGroup("/dev");

    dev.MapPost("/datos-demo", async (DbContextOptions<AppDbContext> options, CancellationToken ct) =>
        Results.Ok(await DatosDemo.CrearAsync(options, ct)));

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

    simular.MapPost("/recepcion", async (ITenantActual tenant, DbContextOptions<AppDbContext> options, CancellationToken ct) =>
    {
        if (tenant.Contexto is not { } contexto) return Results.Forbid();
        return await DatosDemo.SimularRecepcionAsync(options, contexto.OperadorId, ct)
            ? Results.NoContent()
            : Results.NotFound();
    });
}

app.Run();
