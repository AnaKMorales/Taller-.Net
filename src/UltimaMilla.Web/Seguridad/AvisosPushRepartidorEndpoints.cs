using UltimaMilla.Application.Notificaciones;
using UltimaMilla.Infrastructure.Push;
using UltimaMilla.Web.Api;

namespace UltimaMilla.Web.Seguridad;

public sealed record ClavePushRepartidor(string ClavePublica);

// La PWA registra acá su suscripción Web Push para recibir avisos con la app cerrada (docs/push-repartidor.md).
public static class AvisosPushRepartidorEndpoints
{
    // Web solo necesita la clave pública (WebPush__ClavePublica). La privada queda en el Worker.
    public static void AgregarAvisosPushRepartidor(this IServiceCollection servicios, IConfiguration configuracion) =>
        servicios.AgregarSuscripcionesPush(configuracion.GetSection(OpcionesWebPush.Seccion).Get<OpcionesWebPush>());

    public static void MapearAvisosPushRepartidor(this RouteGroupBuilder api)
    {
        var grupo = api.MapGroup("/avisos").RequireAuthorization(Politicas.Repartidor)
            .AddEndpointFilter<ErroresApiFilter>();

        // 404: el servidor no tiene Web Push configurado; la PWA lo informa y sigue funcionando sin avisos.
        grupo.MapGet("/clave", (ISuscripcionesPushRepartidor suscripciones) =>
            suscripciones.ClavePublica is { } clave ? Results.Ok(new ClavePushRepartidor(clave)) : Results.NotFound());

        grupo.MapPost("/suscripcion", async (SuscripcionPushDto solicitud, ISuscripcionesPushRepartidor suscripciones,
            CancellationToken ct) =>
        {
            await suscripciones.GuardarAsync(solicitud, ct);
            return Results.NoContent();
        });

        // POST (no DELETE): la política CORS de la PWA solo admite GET y POST.
        grupo.MapPost("/suscripcion/baja", async (BajaSuscripcionPushDto solicitud, ISuscripcionesPushRepartidor suscripciones,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(solicitud.Endpoint)) return Results.BadRequest();
            await suscripciones.QuitarAsync(solicitud.Endpoint, ct);
            return Results.NoContent();
        });
    }
}
