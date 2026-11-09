using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Dispositivo;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Tests;

public sealed class TelemetriaSincronizacionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnFalloAlReportarNoImpideSincronizarLaEntrega(bool fallaAlmacenamiento)
    {
        var almacen = new AlmacenConFalloDeTelemetria(fallaAlmacenamiento);
        var datos = new DatosLocales(almacen);
        await datos.GuardarSesionAsync(Datos.Sesion);
        var cola = new ColaAcciones(almacen);
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        Assert.Empty(await cola.EncolarAsync(Datos.Entrega(hoja.Paradas[0].EnvioId), hoja, Datos.Sesion.RepartidorId));
        var servidor = new ServidorEnMemoria { TelemetriaCaida = !fallaAlmacenamiento };
        var reloj = new RelojManual(Datos.Inicio);
        using var conexion = new EstadoConexion(new Navegador(new JsSinUso()));
        var automatica = new SincronizacionAutomatica(conexion, new Sincronizador(cola, servidor, reloj),
            datos, servidor, reloj, NullLogger<SincronizacionAutomatica>.Instance, cola);

        await automatica.SincronizarAhoraAsync();

        Assert.Equal(1, servidor.Registradas);
        Assert.Equal(EstadoAccion.Sincronizada, Assert.Single(await cola.ListarAsync(Datos.Sesion.RepartidorId)).Estado);
    }

    private sealed class AlmacenConFalloDeTelemetria(bool fallar) : IAlmacenLocal
    {
        private readonly AlmacenEnMemoria _datos = new();
        public Task<string?> LeerAsync(string clave) => fallar && clave == "dispositivo-id"
            ? Task.FromException<string?>(new JSException("IndexedDB no disponible")) : _datos.LeerAsync(clave);
        public Task GuardarAsync(string clave, string valor) => _datos.GuardarAsync(clave, valor);
        public Task BorrarAsync(string clave) => _datos.BorrarAsync(clave);
    }

    private sealed class JsSinUso : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken,
            object?[]? args) => throw new NotSupportedException();
    }
}
