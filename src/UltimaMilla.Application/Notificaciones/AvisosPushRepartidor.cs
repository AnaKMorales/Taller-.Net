using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Notificaciones;

// Avisos push al celular del repartidor (Web Push estándar con VAPID). Llegan aunque la PWA esté cerrada:
// los muestra el service worker. Ver docs/push-repartidor.md.

// Lo que entrega el navegador al suscribirse (PushSubscription): dirección del servicio push y claves del dispositivo.
public sealed record SuscripcionPushDto(string Endpoint, string P256dh, string Auth);

public sealed record BajaSuscripcionPushDto(string Endpoint);

// Suscripciones del repartidor autenticado. Operador y repartidor salen siempre del contexto, nunca del cuerpo.
public interface ISuscripcionesPushRepartidor
{
    // Clave pública VAPID para pushManager.subscribe; null si el servidor no tiene Web Push configurado.
    string? ClavePublica { get; }

    // Alta o renovación (idempotente por Endpoint). Lanza ArgumentException si los datos no son válidos.
    Task GuardarAsync(SuscripcionPushDto suscripcion, CancellationToken ct = default);

    Task QuitarAsync(string endpoint, CancellationToken ct = default);
}

// Contenido que lee el service worker (wwwroot/service-worker*.js). Se cifra de punta a punta: el servicio push
// (Google, Mozilla, Apple) no lo ve.
public sealed record AvisoPush(string Titulo, string Cuerpo, string Etiqueta, Guid EnvioId, DateTime? LlegadaEstimada);

public static class MensajesPushRepartidor
{
    // Solo las asignaciones a una ruta le cambian el trabajo al repartidor.
    public static bool EsRelevante(string tipoEvento) =>
        tipoEvento is nameof(TipoEvento.Asignar) or nameof(TipoEvento.Reasignar);

    public static AvisoPush Componer(string tipoEvento, Guid envioId, string codigoSeguimiento, int orden,
        DateTime llegadaEstimada)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoSeguimiento);
        var reasignado = tipoEvento == nameof(TipoEvento.Reasignar);
        var codigo = codigoSeguimiento.Length > 8 ? codigoSeguimiento[..8].ToUpperInvariant() : codigoSeguimiento;
        return new AvisoPush(
            reasignado ? "Envío reasignado a tu ruta" : "Nuevo envío en tu ruta",
            $"Envío {codigo} · parada {orden}. Abrí la app para ver la hoja de ruta.",
            // Misma etiqueta para el mismo envío: si llega dos veces, el celular muestra una sola notificación.
            $"envio-{envioId:N}",
            envioId,
            DateTime.SpecifyKind(llegadaEstimada, DateTimeKind.Utc));
    }
}
