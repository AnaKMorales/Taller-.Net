using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Notificaciones;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Usuarios;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Push;

namespace UltimaMilla.Infrastructure.Push;

// Alta y baja de los dispositivos del repartidor autenticado (lo usa la API de la PWA en Web).
public sealed class SuscripcionesPushRepartidor(DbContextOptions<AppDbContext> options, ITenantActual actual,
    OpcionesWebPush configuracion, TimeProvider reloj) : ISuscripcionesPushRepartidor
{
    public string? ClavePublica => configuracion.PuedeSuscribir ? configuracion.ClavePublica : null;

    public async Task GuardarAsync(SuscripcionPushDto suscripcion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(suscripcion);
        Validar(suscripcion);
        try { await IntentarGuardarAsync(suscripcion, ct); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Dos altas del mismo dispositivo a la vez: la segunda pasa a ser una renovación.
            await IntentarGuardarAsync(suscripcion, ct);
        }
    }

    public async Task QuitarAsync(string endpoint, CancellationToken ct = default)
    {
        var (tenant, usuarioId) = Repartidor();
        await using var db = new AppDbContext(options, new TenantActual(tenant));
        await db.Set<SuscripcionPush>().Where(s => s.Endpoint == endpoint && s.Usuario.Id == usuarioId)
            .ExecuteDeleteAsync(ct);
    }

    private async Task IntentarGuardarAsync(SuscripcionPushDto suscripcion, CancellationToken ct)
    {
        var (tenant, usuarioId) = Repartidor();
        await using var db = new AppDbContext(options, new TenantActual(tenant));
        var repartidor = await db.Set<Repartidor>()
            .SingleOrDefaultAsync(r => r.Id == usuarioId && r.Activo && r.Rol == RolUsuario.Repartidor, ct)
            ?? throw new AislamientoOperadorException();
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var existente = await db.Set<SuscripcionPush>().SingleOrDefaultAsync(s => s.Endpoint == suscripcion.Endpoint, ct);
        if (existente is null)
            db.Add(new SuscripcionPush(repartidor, suscripcion.Endpoint, suscripcion.P256dh, suscripcion.Auth, ahora));
        else
            existente.Renovar(repartidor, suscripcion.P256dh, suscripcion.Auth, ahora);
        await db.SaveChangesAsync(ct);
    }

    private (TenantContext Tenant, Guid UsuarioId) Repartidor() =>
        actual.Contexto is { Origen: OrigenTenant.UsuarioAutenticado, Rol: RolUsuario.Repartidor, UsuarioId: Guid id } tenant
            ? (tenant, id) : throw new AislamientoOperadorException();

    private void Validar(SuscripcionPushDto s)
    {
        if (string.IsNullOrWhiteSpace(s.Endpoint) || s.Endpoint.Length > SuscripcionPush.LargoMaximoEndpoint ||
            !Uri.TryCreate(s.Endpoint, UriKind.Absolute, out var endpoint) || !configuracion.EsServicioPermitido(endpoint))
            throw new ArgumentException("El servicio push del dispositivo no está permitido.");
        if (!Base64Url.Intentar(s.P256dh, out var p256dh) || p256dh is not { Length: 65 } || p256dh[0] != 0x04 ||
            s.P256dh.Length > 128)
            throw new ArgumentException("La clave p256dh del dispositivo no es válida.");
        if (!Base64Url.Intentar(s.Auth, out var auth) || auth.Length != 16 || s.Auth.Length > 64)
            throw new ArgumentException("El secreto auth del dispositivo no es válido.");
    }
}
