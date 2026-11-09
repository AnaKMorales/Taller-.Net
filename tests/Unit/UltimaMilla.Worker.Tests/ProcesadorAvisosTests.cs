using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Worker.Tests;

public sealed class ProcesadorAvisosTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Resolver de prueba: devuelve un TenantContext NUEVO por llamada y recuerda qué se le pidió.
    private sealed class Resolver : ITenantResolver
    {
        public ConcurrentQueue<TenantMessageScope> Pedidos { get; } = new();
        public Func<TenantMessageScope, Exception?>? Falla { get; set; }

        public Task<TenantContext> DesdeMensajeAsync(TenantMessageScope mensaje, CancellationToken cancellationToken = default)
        {
            Pedidos.Enqueue(mensaje);
            if (Falla?.Invoke(mensaje) is { } error) throw error;
            return Task.FromResult(TenantContext.ParaMensaje(mensaje.OperadorId, mensaje.CuentaComercialId));
        }

        public Task<TenantContext> DesdeClaimsAsync(System.Security.Claims.ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    // Cumple el contrato de IRegistroIdempotencia: reserva por (MensajeId, Consumidor) y la deshace si la acción falla.
    private sealed class Registro : IRegistroIdempotencia
    {
        private readonly object _candado = new();
        private readonly HashSet<(Guid, string)> _procesados = [];
        public ConcurrentQueue<(Guid MensajeId, string Consumidor)> Llamadas { get; } = new();

        public async Task<bool> EjecutarUnaVezAsync(TenantContext tenant, Guid mensajeId, string consumidor,
            Func<CancellationToken, Task> accion, CancellationToken cancellationToken = default)
        {
            Llamadas.Enqueue((mensajeId, consumidor));
            lock (_candado) { if (_procesados.Contains((mensajeId, consumidor))) return false; }
            await accion(cancellationToken); // si lanza, no se marca
            lock (_candado) { return _procesados.Add((mensajeId, consumidor)); }
        }
    }

    private sealed class Manejador : IManejadorEventoEnvio
    {
        public ConcurrentQueue<(EventoEnvioV1 Evento, TenantContext Tenant)> Recibidos { get; } = new();
        public Func<EventoEnvioV1, Task>? Accion { get; set; }

        public async Task ManejarAsync(EventoEnvioV1 evento, TenantContext tenant, CancellationToken cancellationToken = default)
        {
            if (Accion is not null) await Accion(evento);
            Recibidos.Enqueue((evento, tenant));
        }
    }

    private readonly Resolver _resolver = new();
    private readonly Registro _registro = new();
    private readonly Manejador _manejador = new();

    private ProcesadorAvisos Crear() => new(_resolver, _registro, _manejador, NullLogger<ProcesadorAvisos>.Instance);

    private static EventoEnvioV1 Evento(Guid? operador = null, Guid? cuenta = null, Guid? mensaje = null, int version = 1) =>
        new(mensaje ?? Guid.NewGuid(), version, operador ?? Guid.NewGuid(), cuenta ?? Guid.NewGuid(), Guid.NewGuid(),
            mensaje ?? Guid.NewGuid(), "CambioDeEstado", "Sistema", "Recibido", "EnDeposito", DateTime.UtcNow, null);

    private static MensajeRecibido Mensaje(EventoEnvioV1 e, string? messageId = "*", int intentos = 0) =>
        new(messageId == "*" ? e.MensajeId.ToString() : messageId, "envio.evento.v1", JsonSerializer.Serialize(e, Json), intentos);

    // ---- Idempotencia ----

    [Fact]
    public async Task MensajeNuevoSeProcesaUnaVezConSuTenant()
    {
        var evento = Evento();
        var resultado = await Crear().ManejarAsync(Mensaje(evento));

        Assert.Equal(ResultadoConsumo.Confirmar, resultado);
        var (recibido, tenant) = Assert.Single(_manejador.Recibidos);
        Assert.Equal(evento, recibido);
        Assert.Equal(evento.OperadorId, tenant.OperadorId);
        Assert.Equal(evento.CuentaComercialId, tenant.CuentaComercialId);
        Assert.Equal(OrigenTenant.MensajeWorker, tenant.Origen);
        Assert.Equal((evento.MensajeId, "avisos"), Assert.Single(_registro.Llamadas));
    }

    [Fact]
    public async Task UnDuplicadoSeConfirmaSinVolverAProcesar()
    {
        var evento = Evento();
        var procesador = Crear();

        Assert.Equal(ResultadoConsumo.Confirmar, await procesador.ManejarAsync(Mensaje(evento)));
        Assert.Equal(ResultadoConsumo.Confirmar, await procesador.ManejarAsync(Mensaje(evento)));
        Assert.Equal(ResultadoConsumo.Confirmar, await procesador.ManejarAsync(Mensaje(evento, intentos: 2)));

        Assert.Single(_manejador.Recibidos);
        Assert.Equal(3, _registro.Llamadas.Count);
    }

    [Fact]
    public async Task MensajesDistintosSeProcesanCadaUno()
    {
        var procesador = Crear();
        await procesador.ManejarAsync(Mensaje(Evento()));
        await procesador.ManejarAsync(Mensaje(Evento()));
        Assert.Equal(2, _manejador.Recibidos.Count);
    }

    [Fact]
    public async Task SiElManejadorFallaSeReintentaYElReintentoSiProcesa()
    {
        var evento = Evento();
        var fallar = true;
        _manejador.Accion = _ => fallar ? throw new IOException("servicio caído") : Task.CompletedTask;
        var procesador = Crear();

        Assert.Equal(ResultadoConsumo.Reintentar, await procesador.ManejarAsync(Mensaje(evento)));
        Assert.Empty(_manejador.Recibidos);

        fallar = false; // el mensaje vuelve de la cola de espera: la falla no lo dejó marcado como procesado
        Assert.Equal(ResultadoConsumo.Confirmar, await procesador.ManejarAsync(Mensaje(evento, intentos: 1)));
        Assert.Single(_manejador.Recibidos);
    }

    [Fact]
    public async Task UnWebhookFallidoDisparaElReintentoYElMensajeNoQuedaProcesado()
    {
        var evento = Evento();
        var falla = true;
        _manejador.Accion = e => falla
            ? throw new UltimaMilla.Application.Avisos.AvisoWebhookFallidoException(e.EventoId, [Guid.NewGuid()])
            : Task.CompletedTask;
        var procesador = Crear();

        Assert.Equal(ResultadoConsumo.Reintentar, await procesador.ManejarAsync(Mensaje(evento)));
        falla = false; // el comercio se recuperó: el mensaje vuelve de la cola de espera
        Assert.Equal(ResultadoConsumo.Confirmar, await procesador.ManejarAsync(Mensaje(evento, intentos: 1)));
        Assert.Single(_manejador.Recibidos);
    }

    [Fact]
    public async Task UnFalloTransitorioAlResolverElTenantSeReintenta()
    {
        _resolver.Falla = _ => new TimeoutException("base de datos");
        Assert.Equal(ResultadoConsumo.Reintentar, await Crear().ManejarAsync(Mensaje(Evento())));
        Assert.Empty(_registro.Llamadas);
    }

    // ---- Contrato V2 (entrega por bulto) ----

    [Fact]
    public async Task UnEventoV2SeProcesaYLlegaAlManejadorEnFormatoV1()
    {
        var v2 = new EventoEnvioV2(Guid.NewGuid(), EventoEnvioV2.VersionContrato, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "EntregaParcial", "AppRepartidor", "EnTransito", "EntregadoParcialmente",
            DateTime.UtcNow, Guid.NewGuid(),
            [new ResultadoBultoV2(Guid.NewGuid(), "Entregado", null, [Guid.NewGuid()]),
             new ResultadoBultoV2(Guid.NewGuid(), "NoEntregado", Guid.NewGuid(), [])]);
        var mensaje = new MensajeRecibido(v2.MensajeId.ToString(), EventoEnvioV2.TipoMensaje, JsonSerializer.Serialize(v2, Json), 0);

        Assert.Equal(ResultadoConsumo.Confirmar, await Crear().ManejarAsync(mensaje));

        var (recibido, tenant) = Assert.Single(_manejador.Recibidos);
        Assert.Equal(new EventoEnvioV1(v2.MensajeId, EventoEnvioV1.VersionContrato, v2.OperadorId, v2.CuentaComercialId,
            v2.EnvioId, v2.EventoId, v2.TipoEvento, v2.Origen, v2.EstadoAnterior, v2.EstadoNuevo, v2.OcurridoEn,
            v2.IdOperacion), recibido);
        Assert.Equal(v2.CuentaComercialId, tenant.CuentaComercialId);
        Assert.Equal((v2.MensajeId, "avisos"), Assert.Single(_registro.Llamadas));
    }

    [Fact]
    public async Task UnEventoV2DuplicadoNoSeVuelveAProcesar()
    {
        var v2 = new EventoEnvioV2(Guid.NewGuid(), EventoEnvioV2.VersionContrato, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "EntregaExitosa", "AppRepartidor", "EnTransito", "Entregado",
            DateTime.UtcNow, Guid.NewGuid(), []);
        var mensaje = new MensajeRecibido(v2.MensajeId.ToString(), EventoEnvioV2.TipoMensaje, JsonSerializer.Serialize(v2, Json), 0);
        var procesador = Crear();

        Assert.Equal(ResultadoConsumo.Confirmar, await procesador.ManejarAsync(mensaje));
        Assert.Equal(ResultadoConsumo.Confirmar, await procesador.ManejarAsync(mensaje with { Intentos = 1 }));
        Assert.Single(_manejador.Recibidos);
    }

    // ---- Mensajes inválidos / tenant inválido ----

    [Fact]
    public async Task OperadorOCuentaInexistenteSeDescartaSinTocarLaIdempotencia()
    {
        _resolver.Falla = _ => new AislamientoOperadorException();
        Assert.Equal(ResultadoConsumo.Descartar, await Crear().ManejarAsync(Mensaje(Evento())));
        Assert.Empty(_registro.Llamadas);
        Assert.Empty(_manejador.Recibidos);
    }

    [Fact]
    public async Task MensajesInvalidosSeDescartanSinResolverTenant()
    {
        var bueno = Evento();
        var casos = new[]
        {
            new MensajeRecibido(bueno.MensajeId.ToString(), "t", "esto no es json", 0),
            new MensajeRecibido(bueno.MensajeId.ToString(), "t", "null", 0),
            new MensajeRecibido(bueno.MensajeId.ToString(), "t", "{}", 0),
            Mensaje(Evento(version: 2)),                   // dice ser V2 pero no trae los bultos
            Mensaje(Evento(version: 3)),                   // versión desconocida
            Mensaje(Evento(operador: Guid.Empty)),
            Mensaje(Evento(cuenta: Guid.Empty)),
            Mensaje(bueno, messageId: null),              // sin message-id
            Mensaje(bueno, messageId: "no-es-un-guid"),
            Mensaje(bueno, messageId: Guid.NewGuid().ToString()), // message-id distinto del contenido
        };
        var procesador = Crear();
        foreach (var caso in casos)
            Assert.Equal(ResultadoConsumo.Descartar, await procesador.ManejarAsync(caso));
        Assert.Empty(_resolver.Pedidos);
        Assert.Empty(_registro.Llamadas);
    }

    [Fact]
    public async Task LaCancelacionPorApagadoSePropagaYNoSeReintentaNiSeDescarta()
    {
        using var cancelar = new CancellationTokenSource();
        _manejador.Accion = async _ => { await cancelar.CancelAsync(); cancelar.Token.ThrowIfCancellationRequested(); };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Crear().ManejarAsync(Mensaje(Evento()), cancelar.Token));
    }

    // ---- Sin tenant compartido entre mensajes ----

    [Fact]
    public async Task CadaMensajeObtieneSuPropioTenantYNoSeFiltraAlSiguiente()
    {
        var a = Evento(); var b = Evento();
        var procesador = Crear();

        await procesador.ManejarAsync(Mensaje(a));
        await procesador.ManejarAsync(Mensaje(b));

        Assert.Equal([new TenantMessageScope(a.OperadorId, a.CuentaComercialId), new TenantMessageScope(b.OperadorId, b.CuentaComercialId)],
            _resolver.Pedidos.ToArray());
        var recibidos = _manejador.Recibidos.ToArray();
        Assert.Equal(a.OperadorId, recibidos[0].Tenant.OperadorId);
        Assert.Equal(b.OperadorId, recibidos[1].Tenant.OperadorId);
        Assert.NotSame(recibidos[0].Tenant, recibidos[1].Tenant);
        Assert.NotEqual(recibidos[0].Tenant.CuentaComercialId, recibidos[1].Tenant.CuentaComercialId);
    }

    [Fact]
    public async Task ElMismoOperadorTambienRecibeUnContextoNuevoPorMensaje()
    {
        var operador = Guid.NewGuid();
        var procesador = Crear();
        await procesador.ManejarAsync(Mensaje(Evento(operador)));
        await procesador.ManejarAsync(Mensaje(Evento(operador)));
        var recibidos = _manejador.Recibidos.ToArray();
        Assert.NotSame(recibidos[0].Tenant, recibidos[1].Tenant);
        Assert.Equal(2, _resolver.Pedidos.Count);
    }

    [Fact]
    public async Task MensajesConcurrentesDeOperadoresDistintosNuncaSeMezclan()
    {
        var rnd = new Random(7);
        _manejador.Accion = async _ => { await Task.Delay(rnd.Next(1, 8)); await Task.Yield(); };
        var procesador = Crear(); // UNA instancia compartida, como en el Worker
        var eventos = Enumerable.Range(0, 60).Select(_ => Evento()).ToArray();

        var resultados = await Task.WhenAll(eventos.Select(e => procesador.ManejarAsync(Mensaje(e))));

        Assert.All(resultados, r => Assert.Equal(ResultadoConsumo.Confirmar, r));
        Assert.Equal(60, _manejador.Recibidos.Count);
        Assert.All(_manejador.Recibidos, r =>
        {
            Assert.Equal(r.Evento.OperadorId, r.Tenant.OperadorId);
            Assert.Equal(r.Evento.CuentaComercialId, r.Tenant.CuentaComercialId);
        });
        Assert.Equal(60, _manejador.Recibidos.Select(r => r.Tenant).Distinct().Count());
    }

    [Fact]
    public void ElProcesadorNoGuardaTenantNiMensajesEnCampos()
    {
        var campos = typeof(ProcesadorAvisos).GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.DoesNotContain(campos, c => typeof(TenantContext).IsAssignableFrom(c.FieldType) ||
            typeof(TenantMessageScope).IsAssignableFrom(c.FieldType) || typeof(EventoEnvioV1).IsAssignableFrom(c.FieldType) ||
            typeof(MensajeRecibido).IsAssignableFrom(c.FieldType));
        // Solo dependencias inyectadas (sin estado) y la configuración estática de JSON.
        var permitidos = new[] { typeof(ITenantResolver), typeof(IRegistroIdempotencia), typeof(IManejadorEventoEnvio),
            typeof(Microsoft.Extensions.Logging.ILogger<ProcesadorAvisos>), typeof(JsonSerializerOptions), typeof(string) };
        Assert.All(campos, c => Assert.Contains(c.FieldType, permitidos));
    }
}
