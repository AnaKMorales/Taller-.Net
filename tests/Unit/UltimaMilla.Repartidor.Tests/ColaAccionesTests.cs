using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Tests;

public sealed class ColaAccionesTests
{
    private readonly AlmacenEnMemoria _almacen = new();

    [Fact]
    public async Task UnaAccionValidaQuedaPendienteYSobreviveAReabrirLaApp()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        var accion = Datos.Entrega(hoja.Paradas[0].EnvioId);

        Assert.Empty(await new ColaAcciones(_almacen).EncolarAsync(accion, hoja));

        var reabierta = await new ColaAcciones(_almacen).ListarAsync();
        var item = Assert.Single(reabierta);
        Assert.Equal(EstadoAccion.Pendiente, item.Estado);
        Assert.Equal(accion.IdOperacion, item.Accion.IdOperacion);
        Assert.Equal(accion.OcurridoEn, item.Accion.OcurridoEn);
    }

    [Fact]
    public async Task NoSeRegistranDosAccionesParaElMismoEnvio()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        var cola = new ColaAcciones(_almacen);
        await cola.EncolarAsync(Datos.Entrega(hoja.Paradas[0].EnvioId), hoja);

        var errores = await cola.EncolarAsync(Datos.Fallo(hoja.Paradas[0].EnvioId, Datos.Ausente.Id), hoja);

        Assert.Contains("El envío ya tiene una acción registrada.", errores);
        Assert.Single(await cola.ListarAsync());
    }

    [Fact]
    public async Task ElMismoIdOperacionNoSeEncolaDosVeces()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        var cola = new ColaAcciones(_almacen);
        var accion = Datos.Entrega(hoja.Paradas[0].EnvioId);
        await cola.EncolarAsync(accion, hoja);

        Assert.Contains("Esta acción ya está registrada.", await cola.EncolarAsync(accion, hoja));
    }

    [Fact]
    public async Task UnaAccionRechazadaPermiteRegistrarOtraYSePuedeDescartar()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        var cola = new ColaAcciones(_almacen);
        var primera = Datos.Entrega(hoja.Paradas[0].EnvioId);
        await cola.EncolarAsync(primera, hoja);
        await cola.ActualizarAsync(new AccionPendiente(primera, EstadoAccion.Rechazada));

        Assert.Empty(await cola.EncolarAsync(Datos.Fallo(hoja.Paradas[0].EnvioId, Datos.Ausente.Id), hoja));
        await cola.DescartarRechazadaAsync(primera.IdOperacion);

        Assert.DoesNotContain(await cola.ListarAsync(), a => a.Accion.IdOperacion == primera.IdOperacion);
    }

    [Fact]
    public async Task NoSeDescartaUnaAccionPendiente()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        var cola = new ColaAcciones(_almacen);
        var accion = Datos.Entrega(hoja.Paradas[0].EnvioId);
        await cola.EncolarAsync(accion, hoja);

        await cola.DescartarRechazadaAsync(accion.IdOperacion);

        Assert.Single(await cola.ListarAsync());
    }

    [Fact]
    public async Task UnaAccionInvalidaNoSeEncola()
    {
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        var cola = new ColaAcciones(_almacen);

        Assert.NotEmpty(await cola.EncolarAsync(Datos.Fallo(hoja.Paradas[0].EnvioId, motivoId: null), hoja));
        Assert.Empty(await cola.ListarAsync());
    }
}
