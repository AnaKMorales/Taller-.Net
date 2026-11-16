using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Dispositivo;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Tests;

public sealed class SeguimientoPosicionTests
{
    private sealed class Ubicacion(GeoPuntoDto? punto) : IUbicacionDispositivo
    {
        public GeoPuntoDto? Punto { get; set; } = punto;
        public Task<GeoPuntoDto?> ObtenerUbicacionAsync() => Task.FromResult(Punto);
    }

    private sealed class ServidorPosiciones : IApiPosiciones
    {
        public bool SinConexion { get; set; }
        public List<LotePosicionesDto> Lotes { get; } = [];
        public Task<PosicionesRegistradasDto> EnviarPosicionesAsync(SesionRepartidor sesion, LotePosicionesDto lote,
            CancellationToken ct = default)
        {
            if (SinConexion) throw new HttpRequestException("Sin conexión");
            Lotes.Add(lote);
            return Task.FromResult(new PosicionesRegistradasDto(lote.Lecturas.Count, 0, 0));
        }
    }

    private static async Task<(SeguimientoPosicion Seguimiento, ColaPosiciones Cola, ServidorPosiciones Servidor,
        Ubicacion Ubicacion, RelojManual Reloj, HojaDeRutaDto Hoja)> Preparar(bool conSesion = true, bool conHoja = true)
    {
        var almacen = new AlmacenEnMemoria();
        var datos = new DatosLocales(almacen);
        var hoja = Datos.Hoja(EstadoEnvio.EnTransito);
        if (conSesion) await datos.GuardarSesionAsync(Datos.Sesion);
        if (conSesion && conHoja) await datos.GuardarHojaAsync(Datos.Sesion, hoja);
        var cola = new ColaPosiciones(almacen);
        var servidor = new ServidorPosiciones();
        var ubicacion = new Ubicacion(Datos.Posicion);
        var reloj = new RelojManual(Datos.Inicio);
        return (new SeguimientoPosicion(datos, cola, ubicacion, servidor, reloj, NullLogger<SeguimientoPosicion>.Instance),
            cola, servidor, ubicacion, reloj, hoja);
    }

    [Fact]
    public async Task GuardaLaLecturaConLaRutaDeLaHojaYLaHoraDelDispositivoEnUtc()
    {
        var (seguimiento, cola, _, _, reloj, hoja) = await Preparar();

        Assert.True(await seguimiento.RegistrarLecturaAsync());

        var lectura = Assert.Single(await cola.PrimerasAsync(Datos.Sesion.RepartidorId, 10));
        Assert.Equal(hoja.RutaId, lectura.RutaId);
        Assert.Equal(Datos.Posicion.Latitud, lectura.Latitud);
        Assert.Equal(Datos.Posicion.Longitud, lectura.Longitud);
        Assert.Equal(reloj.GetUtcNow().UtcDateTime, lectura.RegistradaEn);
        Assert.Equal(DateTimeKind.Utc, lectura.RegistradaEn.Kind);
        Assert.NotEqual(Guid.Empty, lectura.IdLectura);
    }

    [Theory]
    [InlineData(false, true)]  // sin sesión
    [InlineData(true, false)]  // sin hoja de ruta descargada
    public async Task NoRegistraSinSesionOSinHoja(bool conSesion, bool conHoja)
    {
        var (seguimiento, cola, _, _, _, _) = await Preparar(conSesion, conHoja);
        Assert.False(await seguimiento.RegistrarLecturaAsync());
        Assert.Empty(await cola.PrimerasAsync(Datos.Sesion.RepartidorId, 10));
    }

    [Fact]
    public async Task ConLaHojaVaciaSinRutaNoRegistra()
    {
        var almacen = new AlmacenEnMemoria();
        var datos = new DatosLocales(almacen);
        await datos.GuardarSesionAsync(Datos.Sesion);
        await datos.GuardarHojaAsync(Datos.Sesion, new HojaDeRutaDto(Guid.Empty, Datos.Inicio, [], [], []));
        var cola = new ColaPosiciones(almacen);
        var seguimiento = new SeguimientoPosicion(datos, cola, new Ubicacion(Datos.Posicion), new ServidorPosiciones(),
            new RelojManual(Datos.Inicio), NullLogger<SeguimientoPosicion>.Instance);

        Assert.False(await seguimiento.RegistrarLecturaAsync());
        Assert.Empty(await cola.PrimerasAsync(Datos.Sesion.RepartidorId, 10));
    }

    [Fact]
    public async Task SinPermisoOSenalNoGuardaNadaYNoFalla()
    {
        var (seguimiento, cola, _, ubicacion, _, _) = await Preparar();
        ubicacion.Punto = null;
        Assert.False(await seguimiento.RegistrarLecturaAsync());
        Assert.Empty(await cola.PrimerasAsync(Datos.Sesion.RepartidorId, 10));
    }

    [Fact]
    public async Task SinConexionLasLecturasEsperanYDespuesSalenUnaSolaVez()
    {
        var (seguimiento, cola, servidor, _, reloj, _) = await Preparar();
        await seguimiento.RegistrarLecturaAsync();
        reloj.Ahora += SeguimientoPosicion.Intervalo;
        await seguimiento.RegistrarLecturaAsync();

        servidor.SinConexion = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => seguimiento.EnviarPendientesAsync(Datos.Sesion));
        Assert.Equal(2, (await cola.PrimerasAsync(Datos.Sesion.RepartidorId, 10)).Count);

        servidor.SinConexion = false;
        await seguimiento.EnviarPendientesAsync(Datos.Sesion);
        await seguimiento.EnviarPendientesAsync(Datos.Sesion);

        var lote = Assert.Single(servidor.Lotes);
        Assert.Equal(2, lote.Lecturas.Count);
        Assert.True(lote.Lecturas[0].RegistradaEn < lote.Lecturas[1].RegistradaEn);
        Assert.Empty(await cola.PrimerasAsync(Datos.Sesion.RepartidorId, 10));
    }

    [Fact]
    public async Task EnviaEnLotesAcotados()
    {
        var (seguimiento, cola, servidor, _, _, hoja) = await Preparar();
        for (var i = 0; i < SeguimientoPosicion.TamanoLote + 5; i++)
            await cola.AgregarAsync(Datos.Sesion.RepartidorId, new LecturaPosicionDto(Guid.NewGuid(), hoja.RutaId,
                1, 1, null, Datos.Inicio.UtcDateTime.AddMinutes(i)));

        await seguimiento.EnviarPendientesAsync(Datos.Sesion);

        Assert.Equal([SeguimientoPosicion.TamanoLote, 5], servidor.Lotes.Select(l => l.Lecturas.Count));
    }

    [Fact]
    public async Task LaColaDescartaLasMasViejasAlSuperarElMaximo()
    {
        var cola = new ColaPosiciones(new AlmacenEnMemoria());
        var repartidor = Guid.NewGuid();
        var primera = Guid.NewGuid();
        await cola.AgregarAsync(repartidor, new LecturaPosicionDto(primera, Guid.NewGuid(), 1, 1, null, Datos.Inicio.UtcDateTime));
        for (var i = 0; i < ColaPosiciones.Maximo; i++)
            await cola.AgregarAsync(repartidor, new LecturaPosicionDto(Guid.NewGuid(), Guid.NewGuid(), 1, 1, null,
                Datos.Inicio.UtcDateTime.AddMinutes(i + 1)));

        var todas = await cola.PrimerasAsync(repartidor, ColaPosiciones.Maximo + 10);
        Assert.Equal(ColaPosiciones.Maximo, todas.Count);
        Assert.DoesNotContain(todas, l => l.IdLectura == primera);
        // Cada repartidor tiene su propia cola en el dispositivo.
        Assert.Empty(await cola.PrimerasAsync(Guid.NewGuid(), 10));
    }
}
