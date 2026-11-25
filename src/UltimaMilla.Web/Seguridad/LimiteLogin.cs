using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace UltimaMilla.Web.Seguridad;

// Límite de intentos de login por IP, el mismo para la Web, la PWA del repartidor y la API de comercios.
// 20 por minuto: frena probar contraseñas en serie y alcanza para una flota que entra desde la misma red del depósito.
public static class LimiteLogin
{
    public const string Politica = "Login";

    public static IServiceCollection AgregarLimiteLogin(this IServiceCollection services, IConfiguration configuracion)
    {
        var permitidos = configuracion.GetValue("Login:IntentosPorMinuto", 20);
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(Politica, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitidos, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        return services;
    }
}
