extern alias Pwa;

using System.Net;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using UltimaMilla.Infrastructure.Persistence.Outbox;
using Pwa::UltimaMilla.Repartidor.Dispositivo;
using Pwa::UltimaMilla.Repartidor.Servidor;
using Pwa::UltimaMilla.Repartidor.Simulacion;
using Pwa::UltimaMilla.Repartidor.Sincronizacion;
using UltimaMilla.Web.Desarrollo;
using Movil = Pwa::UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task PwaReenviaTrasPerderConfirmacionYRechazaOtroIntentoConLaHojaObsoleta()
    {
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false, ambiente: "Development");
        using var web = servidor.Cliente();
        using var creacion = await web.PostAsync("/dev/datos-demo-pwa", null);
        creacion.EnsureSuccessStatusCode();
        var demo = (await creacion.Content.ReadFromJsonAsync<DatosDemoCreados>())!;

        using var red = new RedConRespuestaPerdida(servidor.Server.CreateHandler());
        using var http = new HttpClient(red, disposeHandler: false) { BaseAddress = web.BaseAddress };
        var reloj = TimeProvider.System;
        var almacen = new AlmacenPwa();
        using var conexion = new EstadoConexion(new Navegador(new JsSinNavegadorPwa()));
        var api = new ApiRepartidorHttp(http, new ApiRepartidorFalsa(almacen, conexion), reloj);
        var sesion = await api.IniciarSesionAsync(demo.EmailRepartidor, demo.Contrasena);
        Assert.NotNull(sesion);
        var hoja = await api.ObtenerHojaDeRutaAsync(sesion);
        var parada = Assert.Single(hoja.Paradas);
        var visita = VisitaDemoPwa(parada, reloj.GetUtcNow());
        var idCompetidor = Guid.NewGuid();
        var competidora = visita with
        {
            IdOperacion = idCompetidor,
            Intento = visita.Intento! with { IdOperacion = idCompetidor }
        };
        var colaCompetidora = new ColaAcciones(new AlmacenPwa());
        Assert.Empty(await new ColaAcciones(almacen).EncolarAsync(visita, hoja, sesion.RepartidorId));
        Assert.Empty(await colaCompetidora.EncolarAsync(competidora, hoja, sesion.RepartidorId));
        Assert.Empty(red.Solicitudes);

        // Recrear la cola comprueba que el envío usa la acción serializada en el almacén.
        var cola = new ColaAcciones(almacen);
        var sincronizador = new Sincronizador(cola, api, reloj);
        var sinConfirmacion = await sincronizador.SincronizarAsync(sesion);
        Assert.True(sinConfirmacion.SinConexion);
        var pendiente = Assert.Single(await cola.ListarAsync(sesion.RepartidorId));
        Assert.Equal(EstadoAccion.Pendiente, pendiente.Estado);
        Assert.Equal(1, pendiente.Intentos);

        var confirmada = await sincronizador.SincronizarAsync(sesion, ignorarEspera: true);
        Assert.Equal(1, confirmada.Sincronizadas);
        var sincronizada = Assert.Single(await cola.ListarAsync(sesion.RepartidorId));
        Assert.Equal(EstadoAccion.Sincronizada, sincronizada.Estado);
        Assert.Equal("El servidor ya la tenía registrada.", sincronizada.Detalle);
        Assert.Equal(red.Solicitudes[0], red.Solicitudes[1]);

        var otroDispositivo = new Sincronizador(colaCompetidora, api, reloj);
        Assert.Equal(1, (await otroDispositivo.SincronizarAsync(sesion)).Rechazadas);
        var rechazada = Assert.Single(await colaCompetidora.ListarAsync(sesion.RepartidorId));
        Assert.Equal(EstadoAccion.Rechazada, rechazada.Estado);
        Assert.Contains("Actualizá la hoja", rechazada.Detalle);
        Assert.Equal(HttpStatusCode.Conflict, red.Estados[^1]);
        await otroDispositivo.SincronizarAsync(sesion, ignorarEspera: true);
        Assert.Equal(3, red.Solicitudes.Count);

        Assert.Empty((await api.ObtenerHojaDeRutaAsync(sesion, hoja.RutaId)).Paradas);
        await using var db = Context(demo.OperadorId);
        var envio = await db.Envios.Include(e => e.Bultos).Include(e => e.Intentos)
            .SingleAsync(e => e.Id == parada.EnvioId);
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, envio.Estado);
        Assert.Equal(visita.IdOperacion, Assert.Single(envio.Intentos).IdOperacion);
        Assert.Equal(EstadoBulto.Entregado, envio.Bultos.Single(b => b.Codigo == "PARCIAL-01").Estado);
        Assert.Equal(EstadoBulto.NoEntregado, envio.Bultos.Single(b => b.Codigo == "PARCIAL-02").Estado);
        Assert.Equal(1, await db.Set<MensajeOutbox>().CountAsync(m => m.Evento.IdOperacion == visita.IdOperacion));
        Assert.False(await db.Set<MensajeOutbox>().AnyAsync(m => m.Evento.IdOperacion == idCompetidor));
    }

    private static Movil.AccionEntregaDto VisitaDemoPwa(Movil.ParadaDto parada, DateTimeOffset ahora)
    {
        var posicion = new Movil.GeoPuntoDto(-34.9011m, -56.1645m);
        var fotoEntrega = new Movil.EvidenciaIntentoDto(Guid.NewGuid(), Movil.TipoPrueba.Foto, ahora.UtcDateTime, posicion,
            Archivo: "evidencias/prueba-entrega.jpg");
        var fotoFallo = fotoEntrega with { Referencia = Guid.NewGuid(), Archivo = "evidencias/prueba-fallo.jpg" };
        var bultos = parada.Bultos!.OrderBy(b => b.Codigo).ToArray();
        var motivo = Assert.Single(parada.Reglas!.Motivos);
        var operacion = Guid.NewGuid();
        var intento = new Movil.IntentoPorBultoDto(parada.EnvioId, parada.ParadaId!.Value, parada.VersionEsperadaEnvio!.Value,
            operacion, ahora.UtcDateTime,
            [new(bultos[0].BultoId, Movil.ResultadoIntento.Entregado, null, [fotoEntrega.Referencia]),
             new(bultos[1].BultoId, Movil.ResultadoIntento.Fallido, motivo.Id, [fotoFallo.Referencia])],
            [fotoEntrega, fotoFallo], posicion);
        return new(operacion, parada.EnvioId, Movil.ResultadoIntento.Parcial, null, [], ahora, posicion, intento);
    }

    private sealed class RedConRespuestaPerdida(HttpMessageHandler servidor) : DelegatingHandler(servidor)
    {
        public List<string> Solicitudes { get; } = [];
        public List<HttpStatusCode> Estados { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath != RutaIntentosApi)
                return await base.SendAsync(request, ct);

            Solicitudes.Add(await request.Content!.ReadAsStringAsync(ct));
            var respuesta = await base.SendAsync(request, ct);
            Estados.Add(respuesta.StatusCode);
            if (Solicitudes.Count != 1 || !respuesta.IsSuccessStatusCode) return respuesta;
            respuesta.Dispose();
            throw new HttpRequestException("Se perdió la confirmación después de guardar el intento.");
        }
    }

    private sealed class AlmacenPwa : IAlmacenLocal
    {
        private readonly Dictionary<string, string> _datos = [];
        public Task<string?> LeerAsync(string clave) => Task.FromResult(_datos.GetValueOrDefault(clave));
        public Task GuardarAsync(string clave, string valor) { _datos[clave] = valor; return Task.CompletedTask; }
        public Task BorrarAsync(string clave) { _datos.Remove(clave); return Task.CompletedTask; }
    }

    private sealed class JsSinNavegadorPwa : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => throw new NotSupportedException();
    }
}
