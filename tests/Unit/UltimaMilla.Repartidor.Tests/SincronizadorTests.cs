using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Tests;

public sealed class SincronizadorTests
{
    private readonly AlmacenEnMemoria _almacen = new();
    private readonly ServidorEnMemoria _servidor = new();
    private readonly RelojManual _reloj = new(Datos.Inicio);
    private readonly ColaAcciones _cola;
    private readonly Sincronizador _sincronizador;

    public SincronizadorTests()
    {
        _cola = new ColaAcciones(_almacen);
        _sincronizador = new Sincronizador(_cola, _servidor, _reloj);
    }

    private async Task<AccionEntregaDto> EncolarEntregaAsync(HojaDeRutaDto hoja, int parada)
    {
        var accion = Datos.Entrega(hoja.Paradas[parada].EnvioId, Datos.Inicio.AddMinutes(parada));
        Assert.Empty(await _cola.EncolarAsync(accion, hoja, Datos.Sesion.RepartidorId));
        return accion;
    }

    // Escenario pedido: sin conexión se registran 2 entregas; al volver la red se sincronizan una sola vez.
    [Fact]
    public async Task DosEntregasOfflineSeSincronizanUnaSolaVezAlReconectar()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito, EstadoEnvio.EnTransito);
        _servidor.EnLinea = false;
        await EncolarEntregaAsync(hoja, 0);
        await EncolarEntregaAsync(hoja, 1);

        var sinRed = await _sincronizador.SincronizarAsync(Datos.Sesion);
        Assert.True(sinRed.SinConexion);
        Assert.All(await _cola.ListarAsync(Datos.Sesion.RepartidorId), a => Assert.Equal(EstadoAccion.Pendiente, a.Estado));

        _servidor.EnLinea = true;
        var resumen = await _sincronizador.SincronizarAsync(Datos.Sesion, ignorarEspera: true);
        await _sincronizador.SincronizarAsync(Datos.Sesion, ignorarEspera: true);

        Assert.Equal(2, resumen.Sincronizadas);
        Assert.Equal(2, _servidor.Registradas);
        Assert.Equal(0, _servidor.Duplicadas);
        Assert.All(await _cola.ListarAsync(Datos.Sesion.RepartidorId), a => Assert.Equal(EstadoAccion.Sincronizada, a.Estado));
    }

    // El servidor guardó la acción pero la respuesta se perdió: al reenviar, el mismo IdOperacion no duplica.
    [Fact]
    public async Task ReenviarTrasPerderLaRespuestaNoDuplica()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        await EncolarEntregaAsync(hoja, 0);
        _servidor.PerderProximaRespuesta = true;
        var traza = Assert.Single(await _cola.ListarAsync(Datos.Sesion.RepartidorId)).Accion.TraceParent;

        await _sincronizador.SincronizarAsync(Datos.Sesion);
        Assert.Equal(EstadoAccion.Pendiente, Assert.Single(await _cola.ListarAsync(Datos.Sesion.RepartidorId)).Estado);

        await _sincronizador.SincronizarAsync(Datos.Sesion, ignorarEspera: true);

        var item = Assert.Single(await _cola.ListarAsync(Datos.Sesion.RepartidorId));
        Assert.Equal(EstadoAccion.Sincronizada, item.Estado);
        Assert.Equal(1, _servidor.Registradas);
        Assert.Equal(1, _servidor.Duplicadas);
        Assert.Equal(traza, item.Accion.TraceParent);
    }

    [Fact]
    public async Task UnaAccionAnteriorSinTrazaLaGuardaAntesDeEnviarYLaConservaAlReabrir()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        await EncolarEntregaAsync(hoja, 0);
        var anterior = Assert.Single(await _cola.ListarAsync(Datos.Sesion.RepartidorId));
        await _cola.ActualizarAsync(anterior with { Accion = anterior.Accion with { TraceParent = null } }, Datos.Sesion.RepartidorId);
        _servidor.PerderProximaRespuesta = true;
        await _sincronizador.SincronizarAsync(Datos.Sesion);
        var traza = Assert.Single(await _cola.ListarAsync(Datos.Sesion.RepartidorId)).Accion.TraceParent;
        Assert.True(System.Diagnostics.ActivityContext.TryParse(traza, null, out _));
        var reabierta = new ColaAcciones(_almacen);
        await new Sincronizador(reabierta, _servidor, _reloj).SincronizarAsync(Datos.Sesion, ignorarEspera: true);
        var guardada = Assert.Single(await reabierta.ListarAsync(Datos.Sesion.RepartidorId));
        Assert.Equal(EstadoAccion.Sincronizada, guardada.Estado);
        Assert.Equal(traza, guardada.Accion.TraceParent);
    }

    [Fact]
    public async Task UnFalloDeRedProgramaElReintentoConEsperaCreciente()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        await EncolarEntregaAsync(hoja, 0);
        _servidor.EnLinea = false;

        await _sincronizador.SincronizarAsync(Datos.Sesion);
        var item = Assert.Single(await _cola.ListarAsync(Datos.Sesion.RepartidorId));
        Assert.Equal(1, item.Intentos);
        Assert.Equal(Datos.Inicio + TimeSpan.FromSeconds(5), item.ProximoIntentoEn);

        // Antes de que venza la espera, el temporizador no la reenvía.
        _servidor.EnLinea = true;
        _reloj.Ahora = Datos.Inicio.AddSeconds(2);
        await _sincronizador.SincronizarAsync(Datos.Sesion);
        Assert.Equal(1, _servidor.Llamadas);

        _reloj.Ahora = Datos.Inicio.AddSeconds(6);
        await _sincronizador.SincronizarAsync(Datos.Sesion);
        Assert.Equal(EstadoAccion.Sincronizada, Assert.Single(await _cola.ListarAsync(Datos.Sesion.RepartidorId)).Estado);
    }

    // Con 401 las acciones esperan el nuevo ingreso: no se pierden ni consumen reintentos.
    [Fact]
    public async Task ConLaSesionVencidaLasAccionesSiguenPendientesSinContarReintentos()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito, EstadoEnvio.EnTransito);
        await EncolarEntregaAsync(hoja, 0);
        await EncolarEntregaAsync(hoja, 1);
        _servidor.SesionVencida = true;

        var resumen = await _sincronizador.SincronizarAsync(Datos.Sesion);

        Assert.True(resumen.SesionExpirada);
        Assert.Equal(1, _servidor.Llamadas);
        Assert.All(await _cola.ListarAsync(Datos.Sesion.RepartidorId), a =>
        {
            Assert.Equal(EstadoAccion.Pendiente, a.Estado);
            Assert.Equal(0, a.Intentos);
            Assert.Null(a.ProximoIntentoEn);
        });

        _servidor.SesionVencida = false;
        await _sincronizador.SincronizarAsync(Datos.Sesion);
        Assert.Equal(2, _servidor.Registradas);
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(4, 40)]
    [InlineData(20, 300)]
    public void LaEsperaSeDuplicaHastaElMaximo(int intentos, int segundos) =>
        Assert.Equal(TimeSpan.FromSeconds(segundos), Sincronizador.Espera(intentos));

    [Fact]
    public async Task SinRedSeCortaLaRondaSinIntentarLasSiguientes()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito, EstadoEnvio.EnTransito);
        await EncolarEntregaAsync(hoja, 0);
        await EncolarEntregaAsync(hoja, 1);
        _servidor.EnLinea = false;

        await _sincronizador.SincronizarAsync(Datos.Sesion);

        Assert.Equal(1, _servidor.Llamadas);
    }

    [Fact]
    public async Task UnConflictoQuedaRechazadoYNoSeReintenta()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito, EstadoEnvio.EnTransito);
        var conflictiva = await EncolarEntregaAsync(hoja, 0);
        await EncolarEntregaAsync(hoja, 1);
        _servidor.EnviosConflictivos.Add(conflictiva.EnvioId);

        var resumen = await _sincronizador.SincronizarAsync(Datos.Sesion);
        await _sincronizador.SincronizarAsync(Datos.Sesion, ignorarEspera: true);

        // Un rechazo no frena las demás acciones.
        Assert.Equal(new ResumenSincronizacion(1, 1, false), resumen);
        var rechazada = (await _cola.ListarAsync(Datos.Sesion.RepartidorId)).Single(a => a.Accion.IdOperacion == conflictiva.IdOperacion);
        Assert.Equal(EstadoAccion.Rechazada, rechazada.Estado);
        Assert.Equal("El envío cambió de estado.", rechazada.Detalle);
        Assert.Equal(2, _servidor.Llamadas);
    }

    [Fact]
    public async Task SeEnvianEnElOrdenEnQueOcurrieron()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito, EstadoEnvio.EnTransito);
        var tarde = Datos.Entrega(hoja.Paradas[0].EnvioId, Datos.Inicio.AddMinutes(30));
        var temprano = Datos.Entrega(hoja.Paradas[1].EnvioId, Datos.Inicio.AddMinutes(10));
        await _cola.EncolarAsync(tarde, hoja, Datos.Sesion.RepartidorId);
        await _cola.EncolarAsync(temprano, hoja, Datos.Sesion.RepartidorId);
        var orden = new List<Guid>();
        var servidor = new ServidorQueAnota(orden);

        await new Sincronizador(_cola, servidor, _reloj).SincronizarAsync(Datos.Sesion);

        Assert.Equal([temprano.IdOperacion, tarde.IdOperacion], orden);
    }

    // El evento "online", el temporizador y el botón pueden coincidir: solo una ronda envía.
    [Fact]
    public async Task DosRondasSimultaneasNoEnvianLoMismoDosVeces()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        await EncolarEntregaAsync(hoja, 0);
        var liberar = new TaskCompletionSource();
        _servidor.AlRecibir = () => liberar.Task;

        var primera = _sincronizador.SincronizarAsync(Datos.Sesion);
        var segunda = await _sincronizador.SincronizarAsync(Datos.Sesion);
        liberar.SetResult();
        await primera;

        Assert.Equal(new ResumenSincronizacion(0, 0, false), segunda);
        Assert.Equal(1, _servidor.Llamadas);
    }

    private sealed class ServidorQueAnota(List<Guid> orden) : IApiRepartidor
    {
        public Task ReportarSincronizacionAsync(SesionRepartidor sesion, ReporteSincronizacionDto reporte,
            CancellationToken ct = default) => Task.CompletedTask;
        public Task<SesionRepartidor?> IniciarSesionAsync(string email, string contrasena, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<HojaDeRutaDto> ObtenerHojaDeRutaAsync(SesionRepartidor sesion, Guid? rutaPreferida = null, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<RespuestaAccion> EnviarAccionAsync(SesionRepartidor sesion, AccionEntregaDto accion, CancellationToken ct = default)
        {
            orden.Add(accion.IdOperacion);
            return Task.FromResult(new RespuestaAccion(ResultadoAccion.Aceptada));
        }
    }
}
