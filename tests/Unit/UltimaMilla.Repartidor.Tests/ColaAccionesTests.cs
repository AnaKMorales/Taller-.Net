using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Tests;

public sealed class ColaAccionesTests
{
    [Fact]
    public async Task ConservaLaVisitaParcialPorRepartidorYPermiteUnaNuevaParadaDelMismoEnvio()
    {
        var (hoja, accion) = Datos.VisitaParcial();
        var repartidor = Datos.Sesion.RepartidorId;
        var cola = new ColaAcciones(_almacen);
        Assert.Empty(await cola.EncolarAsync(accion, hoja, repartidor));

        var reabierta = new ColaAcciones(_almacen);
        Assert.Empty(await reabierta.ListarAsync(Guid.NewGuid()));
        var primera = Assert.Single(await reabierta.ListarAsync(repartidor));
        Assert.True(System.Diagnostics.ActivityContext.TryParse(primera.Accion.TraceParent, null, out _));
        Assert.Equal(accion.Intento!.IdOperacion, primera.Accion.Intento!.IdOperacion);
        Assert.Equal(DateTimeKind.Utc, primera.Accion.Intento.OcurridoEn.Kind);
        Assert.Single(primera.Accion.Intento.Evidencias);

        var nueva = hoja.Paradas[0] with { ParadaId = Guid.NewGuid(), VersionEsperadaEnvio = Guid.NewGuid() };
        var nuevaOperacion = Guid.NewGuid();
        var siguiente = accion with
        {
            IdOperacion = nuevaOperacion,
            Intento = accion.Intento with { ParadaId = nueva.ParadaId.Value,
                VersionEsperada = nueva.VersionEsperadaEnvio.Value, IdOperacion = nuevaOperacion }
        };
        Assert.Empty(await reabierta.EncolarAsync(siguiente, hoja with { Paradas = [nueva] }, repartidor));
        Assert.Equal(2, (await reabierta.ListarAsync(repartidor)).Count);
    }

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
