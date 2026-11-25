using System.Collections.Concurrent;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Web.Seguridad;

// Protección de los tres logins (Web /login, PWA /api/repartidor/sesion y API /api/comercios/sesion). Hallazgo H6.
// 1. Límite de tasa por IP (solo POST): frena a quien prueba contraseñas en masa desde un mismo origen.
// 2. Bloqueo por (correo, IP): tras 5 fallos desde una IP para un correo, ESA IP queda bloqueada para ese correo
//    5 minutos. El dueño de la cuenta sigue entrando desde su dispositivo: conocer su correo ya no alcanza para
//    dejarlo afuera. El bloqueo de Identity por cuenta queda como respaldo ante un ataque desde muchas IPs, con un
//    umbral más alto (FabricaCuentasAcceso). Ver docs/proteccion-login.md.
public static class ProteccionLogin
{
    public const string PoliticaTasa = "login";

    public static IServiceCollection AgregarProteccionLogin(this IServiceCollection services, IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(configuracion);
        services.AddSingleton<IntentosLogin>();
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(PoliticaTasa, context => HttpMethods.IsPost(context.Request.Method)
                ? RateLimitPartition.GetFixedWindowLimiter(Origen(context.Connection.RemoteIpAddress),
                    // Se lee al crear cada partición (no al registrar): así respeta la configuración final del host.
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = context.RequestServices.GetRequiredService<IConfiguration>().GetValue("Login:IntentosPorMinuto", 20),
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    })
                // Mostrar el formulario no es un intento de login.
                : RateLimitPartition.GetNoLimiter("sin-limite"));
        });
        return services;
    }

    // IPv4 tal cual; IPv6 agrupada por /64 (un mismo cliente suele tener todo el prefijo y podría rotar direcciones).
    public static string Origen(IPAddress? ip)
    {
        if (ip is null) return "sin-ip";
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6) return ip.ToString();
        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }

    // Valida las credenciales salvo que este origen esté bloqueado para ese correo. La respuesta es la misma en
    // todos los casos (null), como en BuscadorUsuariosLogin: no revela si el correo existe ni si hubo bloqueo.
    public static async Task<UsuarioLogin?> ValidarAsync(this IBuscadorUsuariosLogin usuarios, IntentosLogin intentos,
        HttpContext contexto, string? email, string? contrasena, CancellationToken ct)
    {
        var ip = contexto.Connection.RemoteIpAddress;
        if (intentos.Bloqueado(email, ip)) return null;
        var usuario = await usuarios.ValidarAsync(email ?? "", contrasena ?? "", ct);
        if (usuario is null) intentos.Fallo(email, ip);
        else intentos.Exito(email, ip);
        return usuario;
    }
}

// Fallos de login por (correo, IP), en memoria: el despliegue tiene una sola instancia Web (sección 3 del diseño).
// Con varias instancias habría que llevarlo a Redis. Los registros vencidos se limpian solos al consultarse.
public sealed class IntentosLogin(TimeProvider reloj)
{
    public const int MaximoFallos = 5;
    public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(5);
    private const int MaximoRegistros = 100_000;

    private readonly ConcurrentDictionary<(string Email, string Ip), Registro> _registros = new();

    private sealed record Registro(int Fallos, DateTimeOffset Ultimo);

    public bool Bloqueado(string? email, IPAddress? ip) =>
        _registros.TryGetValue(Clave(email, ip), out var r) && Vigente(r) && r.Fallos >= MaximoFallos;

    public void Fallo(string? email, IPAddress? ip)
    {
        var ahora = reloj.GetUtcNow();
        // Tope de memoria ante un barrido con miles de correos: se descartan los vencidos y, si no alcanza, todo.
        if (_registros.Count >= MaximoRegistros) Limpiar();
        _registros.AddOrUpdate(Clave(email, ip), _ => new Registro(1, ahora),
            (_, r) => Vigente(r) ? r with { Fallos = r.Fallos + 1, Ultimo = ahora } : new Registro(1, ahora));
    }

    public void Exito(string? email, IPAddress? ip) => _registros.TryRemove(Clave(email, ip), out _);

    private bool Vigente(Registro r) => reloj.GetUtcNow() - r.Ultimo < Ventana;

    private void Limpiar()
    {
        foreach (var (clave, registro) in _registros)
            if (!Vigente(registro)) _registros.TryRemove(clave, out _);
        if (_registros.Count >= MaximoRegistros) _registros.Clear();
    }

    private static (string, string) Clave(string? email, IPAddress? ip) =>
        ((email ?? "").Trim().ToUpperInvariant(), ProteccionLogin.Origen(ip));
}
