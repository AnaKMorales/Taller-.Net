using System.Collections.Concurrent;
using System.Text;
using UltimaMilla.ReceptorWebhooks;

// Receptor de webhooks de prueba. Todas las rutas de recepción (POST) verifican la firma y responden 401 si no
// es válida. Cada llamada queda en memoria (GET /recibidos) y en la consola para poder seguir la demostración.
//   POST /ok            200 siempre
//   POST /fail          500 siempre
//   POST /slow?ms=10000 espera ms (por defecto más que el timeout del emisor) y luego 200
//   POST /flaky?n=2     500 las primeras n veces POR EVENTO (X-Event-Id) y luego 200
var builder = WebApplication.CreateBuilder(args);
var secreto = builder.Configuration["Receptor:Secreto"];
if (string.IsNullOrWhiteSpace(secreto))
    throw new InvalidOperationException("Falta Receptor:Secreto (variable de entorno Receptor__Secreto).");
var tolerancia = TimeSpan.FromSeconds(builder.Configuration.GetValue("Receptor:ToleranciaSegundos", 300));

var verificador = new Verificador(secreto, tolerancia, TimeProvider.System);
var historial = new ConcurrentQueue<Recepcion>();
var contadores = new ConcurrentDictionary<(string Ruta, string Evento), int>();
const int MaximoHistorial = 200;

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));
app.MapGet("/recibidos", () => Results.Ok(historial.Reverse()));
app.MapDelete("/recibidos", () => { historial.Clear(); contadores.Clear(); return Results.NoContent(); });

app.MapPost("/ok", Ruta(ctx => Recibir(ctx, "/ok", (_, _) => Task.FromResult(200))));
app.MapPost("/fail", Ruta(ctx => Recibir(ctx, "/fail", (_, _) => Task.FromResult(500))));
app.MapPost("/slow", Ruta(ctx => Recibir(ctx, "/slow", async (_, ct) =>
{
    var ms = int.TryParse(ctx.Request.Query["ms"], out var m) && m > 0 ? Math.Min(m, 120_000) : 10_000;
    try { await Task.Delay(ms, ct); } catch (OperationCanceledException) { /* el emisor ya cortó */ }
    return 200;
})));
app.MapPost("/flaky", Ruta(ctx => Recibir(ctx, "/flaky", (intento, _) =>
{
    var n = int.TryParse(ctx.Request.Query["n"], out var f) && f >= 0 ? f : 2;
    return Task.FromResult(intento <= n ? 500 : 200);
})));

app.Run();

// El cast a Delegate hace que ASP.NET escriba el IResult devuelto (un RequestDelegate lo descartaría).
static Delegate Ruta(Func<HttpContext, Task<IResult>> manejador) => manejador;

async Task<IResult> Recibir(HttpContext ctx, string ruta, Func<int, CancellationToken, Task<int>> decidir)
{
    using var lector = new StreamReader(ctx.Request.Body, new UTF8Encoding(false));
    var cuerpo = await lector.ReadToEndAsync(ctx.RequestAborted);
    var evento = ctx.Request.Headers["X-Event-Id"].ToString();
    var resultado = verificador.Verificar(ctx.Request.Headers["X-Timestamp"], ctx.Request.Headers["X-Signature"], cuerpo);

    int estado, intento = 0;
    if (resultado != ResultadoVerificacion.Valida) estado = 401;
    else
    {
        // Intento número N de este evento en esta ruta (lo que permite simular "falla N veces y luego responde").
        intento = contadores.AddOrUpdate((ruta, evento), 1, (_, actual) => actual + 1);
        estado = await decidir(intento, ctx.RequestAborted);
    }

    var registro = new Recepcion(DateTimeOffset.UtcNow, ruta, evento, intento, resultado.ToString(), estado, cuerpo.Length);
    historial.Enqueue(registro);
    while (historial.Count > MaximoHistorial) historial.TryDequeue(out _);
    // Sin secreto ni cuerpo en la consola: solo lo necesario para seguir la demostración.
    app.Logger.LogInformation("POST {Ruta} evento={Evento} intento={Intento} firma={Firma} -> {Estado}",
        ruta, evento, intento, resultado, estado);
    return Results.StatusCode(estado);
}

public sealed record Recepcion(DateTimeOffset Cuando, string Ruta, string EventoId, int Intento, string Firma, int Estado, int BytesCuerpo);
