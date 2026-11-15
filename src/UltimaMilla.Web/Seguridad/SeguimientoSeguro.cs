using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace UltimaMilla.Web.Seguridad;

// Protección del seguimiento público), único expuesto sin login.
// - Limitación de tasa por IP: frena a quien prueba tokens al azar (enumeración).
// - Cabeceras seguras en todas las respuestas. Referrer-Policy importa en particular:
//   el token viaja en la URL y no debe filtrarse a otros sitios por la cabecera Referer.
public static class SeguimientoSeguro
{
    public const string PoliticaTasa = "seguimiento";

    public static IServiceCollection AgregarLimitesSeguimiento(this IServiceCollection services, IConfiguration configuracion)
    {
        var permitidos = configuracion.GetValue("Seguimiento:PedidosPorMinuto", 30);
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(PoliticaTasa, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitidos,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });
        return services;
    }

    public static IApplicationBuilder UsarCabecerasSeguras(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var h = context.Response.Headers;
            h.XContentTypeOptions = "nosniff";
            h.XFrameOptions = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            // La PWA del repartidor necesita la ubicación; el resto del sitio no.
            h["Permissions-Policy"] = context.Request.Path.StartsWithSegments("/repartidor")
                ? "camera=(), microphone=(), geolocation=(self)"
                : "camera=(), microphone=(), geolocation=()";
            if (context.Request.Path.StartsWithSegments("/seguimiento") ||
                context.Request.Path.StartsWithSegments("/api/seguimiento"))
            {
                // La página es estática (SSR): solo el script de Blazor del propio sitio y sus estilos en línea
                h.ContentSecurityPolicy = "default-src 'none'; script-src 'self'; connect-src 'self'; style-src 'unsafe-inline'; img-src 'self' https:; " +
                    "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
                // Datos de un envío concreto: que no los guarden proxies ni buscadores
                h.CacheControl = "no-store";
                h["X-Robots-Tag"] = "noindex, nofollow";
            }
            return Task.CompletedTask;
        });
        await next();
    });
}
