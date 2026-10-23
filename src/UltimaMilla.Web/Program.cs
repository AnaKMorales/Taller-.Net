using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Cache;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Web.Components;
using UltimaMilla.Web.Desarrollo;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
// Los enums (estado del envío, modalidad) salen como texto en la API: "Admitido" en vez de 0.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Razor Pages para el Backoffice y Blazor para el Portal
builder.Services.AddRazorPages();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("UltimaMilla")));

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
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// Seguimiento público: el destinatario no tiene cuenta, el token del envío alcanza.
app.MapGet("/api/seguimiento/{token}", async (string token, ISeguimientoPublico seguimiento, CancellationToken ct) =>
    await seguimiento.ConsultarAsync(token, ct) is { } envio ? Results.Ok(envio) : Results.NotFound());

if (app.Environment.IsDevelopment())
{
    // Todavía no hay login: estos endpoints existen solo en desarrollo, para probar y mostrar la caché.
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
}

app.Run();
