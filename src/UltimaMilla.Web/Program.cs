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
// Los enums (estado del envío, modalidad) salen como texto en la API: "Admitido" en vez de 0.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Razor Pages para el Backoffice y Blazor para el Portal
builder.Services.AddRazorPages();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Sesión con cookie. La crea /login con email y contraseña; con Identity se mantiene la misma cookie y los mismos claims.
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
    .AddPolicy(Politicas.Portal, p => p.RequireRole(nameof(UltimaMilla.Domain.Usuarios.RolUsuario.Comercio)));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantActual, TenantActualHttp>();

// Cada EventoDeEnvio que se guarda se publica en vivo al tablero de su operador.
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

// Caché del tarifario y del seguimiento público (ADR-004). Sin cadena de Redis usa memoria.
builder.Services.AgregarCacheUltimaMilla(builder.Configuration.GetConnectionString("Redis"),
    builder.Configuration.GetSection("Cache").Get<CacheOpciones>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Sin UseHttpsRedirection: en local se usa HTTP y en AWS el HTTPS lo termina Traefik (sección 3.3).
app.UseAuthentication();

// Resuelve el operador de la sesión una vez por petición y lo deja para AppDbContext (TenantActualHttp).
// TenantResolver vuelve a verificar contra la base que el usuario siga activo y con el mismo rol.
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
            // Usuario desactivado o claims que ya no coinciden: se cierra la sesión.
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

// Seguimiento público: el destinatario no tiene cuenta, el token del envío alcanza.
app.MapGet("/api/seguimiento/{token}", async (string token, ISeguimientoPublico seguimiento, CancellationToken ct) =>
    await seguimiento.ConsultarAsync(token, ct) is { } envio ? Results.Ok(envio) : Results.NotFound());

if (app.Environment.IsDevelopment())
{
    // Endpoints de prueba: existen solo en desarrollo.
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

    // Simulación para el tablero: actúan sobre el operador del usuario con sesión, nunca sobre otro.
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
