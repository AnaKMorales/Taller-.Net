using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Notificaciones;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Inbox;
using UltimaMilla.Infrastructure.Persistence.Push;
using UltimaMilla.Infrastructure.Push;

namespace UltimaMilla.Infrastructure.IntegrationTests;

// Aviso push al repartidor con la app cerrada cuando el Backoffice le asigna un envío: docs/push-repartidor.md.
public sealed partial class PostgresTests
{
    private sealed class ServicioPushFalso(params string[] vencidos) : IClienteWebPush
    {
        public ConcurrentQueue<(Uri Endpoint, AvisoPush Aviso)> Enviados { get; } = new();
        public Task<ResultadoPush> EnviarAsync(Uri endpoint, string p256dh, string auth, byte[] contenido, string etiqueta,
            CancellationToken cancellationToken = default)
        {
            if (vencidos.Contains(endpoint.ToString())) return Task.FromResult(ResultadoPush.Vencida);
            Enviados.Enqueue((endpoint, JsonSerializer.Deserialize<AvisoPush>(contenido, JsonSerializerOptions.Web)!));
            return Task.FromResult(ResultadoPush.Entregado);
        }
    }

    private static OpcionesWebPush ClavesPush()
    {
        using var clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = clave.ExportParameters(true);
        static string B64(byte[] d) => Convert.ToBase64String(d).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new OpcionesWebPush { ClavePublica = B64([0x04, .. p.Q.X!, .. p.Q.Y!]), ClavePrivada = B64(p.D!) };
    }

    private static SuscripcionPushDto Dispositivo(string endpoint)
    {
        using var clave = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p = clave.ExportParameters(false);
        static string B64(byte[] d) => Convert.ToBase64String(d).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new(endpoint, B64([0x04, .. p.Q.X!, .. p.Q.Y!]), B64(RandomNumberGenerator.GetBytes(16)));
    }

    private async Task<(EscenarioPersistencia E, EventoEnvioV1 Evento, TenantContext Tenant)> EnvioAsignadoAsync()
    {
        var e = Escenario();
        await using var db = Context();
        db.AddRange(e.Envio, e.Repartidor, e.Vehiculo);
        await db.SaveChangesAsync();
        e.Recibir(); await db.SaveChangesAsync();
        db.Add(e.Asignar()); await db.SaveChangesAsync();
        var asignacion = e.Envio.Eventos.Single(v => v.Tipo == TipoEvento.Asignar);
        var cuenta = e.Envio.CuentaComercial.Id;
        var evento = new EventoEnvioV1(Guid.NewGuid(), 1, e.Operador.Id, cuenta, e.Envio.Id, asignacion.Id,
            asignacion.Tipo.ToString(), asignacion.Origen.ToString(), asignacion.EstadoAnterior?.ToString(),
            asignacion.EstadoNuevo.ToString(), asignacion.OcurridoEn, asignacion.IdOperacion);
        return (e, evento, TenantContext.ParaMensaje(e.Operador.Id, cuenta));
    }

    private SuscripcionesPushRepartidor SuscripcionesDe(EscenarioPersistencia e, OpcionesWebPush claves, Usuario? usuario = null)
    {
        var quien = usuario ?? e.Repartidor;
        return new(Options(), new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, quien.Id, quien.Rol)), claves,
            TimeProvider.System);
    }

    [PostgresFact]
    public async Task AsignacionAvisaUnaVezACadaDispositivoDelRepartidorYBorraLosVencidos()
    {
        var (e, evento, tenant) = await EnvioAsignadoAsync();
        var claves = ClavesPush();
        var suscripciones = SuscripcionesDe(e, claves);
        await suscripciones.GuardarAsync(Dispositivo("https://fcm.googleapis.com/fcm/send/celular"));
        await suscripciones.GuardarAsync(Dispositivo("https://updates.push.services.mozilla.com/wpush/v2/viejo"));
        // El mismo celular se vuelve a registrar al abrir la app: se renueva, no se duplica.
        await suscripciones.GuardarAsync(Dispositivo("https://fcm.googleapis.com/fcm/send/celular"));
        var servicio = new ServicioPushFalso("https://updates.push.services.mozilla.com/wpush/v2/viejo");
        var aviso = new AvisoPushRepartidor(Options(), new RegistroIdempotencia(Options(), TimeProvider.System), servicio,
            claves, NullLogger<AvisoPushRepartidor>.Instance);

        await aviso.ManejarAsync(evento, tenant);
        await aviso.ManejarAsync(evento, tenant); // reentrega de la cola: no se repite

        var (endpoint, push) = Assert.Single(servicio.Enviados);
        Assert.Equal("https://fcm.googleapis.com/fcm/send/celular", endpoint.ToString());
        Assert.Equal(("Nuevo envío en tu ruta", e.Envio.Id), (push.Titulo, push.EnvioId));
        Assert.Contains(e.Envio.CodigoSeguimiento[..8].ToUpperInvariant(), push.Cuerpo);
        Assert.Contains("parada 1", push.Cuerpo);
        await using var db = Context();
        var quedan = await db.Set<SuscripcionPush>().AsNoTracking().Select(s => s.Endpoint).ToListAsync();
        Assert.Equal(["https://fcm.googleapis.com/fcm/send/celular"], quedan);
    }

    [PostgresFact]
    public async Task SoloElRepartidorRegistraSusDispositivosYSoloEnServiciosPushConocidos()
    {
        var (e, _, _) = await EnvioAsignadoAsync();
        var claves = ClavesPush();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            SuscripcionesDe(e, claves).GuardarAsync(Dispositivo("https://169.254.169.254/latest/meta-data")));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() =>
            SuscripcionesDe(e, claves, e.Admin).GuardarAsync(Dispositivo("https://fcm.googleapis.com/fcm/send/admin")));

        // La baja borra solo la suscripción propia.
        await SuscripcionesDe(e, claves).GuardarAsync(Dispositivo("https://fcm.googleapis.com/fcm/send/celular"));
        await SuscripcionesDe(e, claves).QuitarAsync("https://fcm.googleapis.com/fcm/send/celular");
        await using var db = Context();
        Assert.False(await db.Set<SuscripcionPush>().AnyAsync());
    }
}
