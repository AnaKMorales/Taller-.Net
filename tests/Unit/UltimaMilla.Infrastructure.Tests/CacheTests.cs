using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Cache;

namespace UltimaMilla.Infrastructure.Tests;

public sealed class CacheTests
{
    private static readonly DateTime Ahora = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private readonly CacheEspia _cache = new();
    private readonly CacheMetricas _metricas = new();
    private readonly CacheOpciones _opciones = new();
    private readonly RelojManual _reloj = new();

    [Fact]
    public async Task SeguimientoVaALaBaseSoloLaPrimeraVezYCuentaAciertos()
    {
        var origen = new SeguimientoFalso();
        var token = Guid.NewGuid().ToString("N");
        var seguimiento = Seguimiento(origen);

        var primero = await seguimiento.ConsultarAsync(token);
        var segundo = await seguimiento.ConsultarAsync(token);

        Assert.Equal(1, origen.Llamadas);
        Assert.Equal(primero, segundo);
        Assert.Equal(new ResumenCache("seguimiento", 1, 1, 0, 0.5), _metricas.Resumen().Single());
    }

    [Fact]
    public async Task SeguimientoNoGuardaTokensInexistentesNiMalFormados()
    {
        var origen = new SeguimientoFalso { Existe = false };
        var seguimiento = Seguimiento(origen);
        var token = Guid.NewGuid().ToString("N");

        Assert.Null(await seguimiento.ConsultarAsync(token));
        Assert.Null(await seguimiento.ConsultarAsync(token));
        Assert.Equal(2, origen.Llamadas);
        Assert.Equal(2, _metricas.Resumen().Single().Fallos);

        Assert.Null(await seguimiento.ConsultarAsync("no-es-un-token"));
        Assert.Equal(2, _metricas.Resumen().Single().Fallos);
        Assert.Empty(_cache.Duraciones);
    }

    [Fact]
    public async Task SeguimientoUsaUnHashComoClaveYLaDuracionCorta()
    {
        var token = Guid.NewGuid();
        var seguimiento = Seguimiento(new SeguimientoFalso());

        await seguimiento.ConsultarAsync(token.ToString("N"));
        // El mismo token en mayúsculas es el mismo envío: debe salir de la caché.
        await seguimiento.ConsultarAsync(token.ToString("N").ToUpperInvariant());

        var clave = Assert.Single(_cache.Duraciones).Key;
        Assert.DoesNotContain(token.ToString("N"), clave);
        Assert.Equal(_opciones.DuracionSeguimiento, _cache.Duraciones[clave]);
        Assert.Equal(1, _metricas.Resumen().Single().Aciertos);
    }

    [Fact]
    public async Task TarifarioNoMezclaOperadores()
    {
        var operadorA = Guid.NewGuid();
        var operadorB = Guid.NewGuid();
        var origen = new TarifarioFalso();
        origen.Cuadros[operadorA] = Cuadro(version: 1);
        origen.Cuadros[operadorB] = Cuadro(version: 7);
        var tarifario = Tarifario(origen);

        Assert.Equal(1, (await tarifario.ObtenerAsync(operadorA))!.Version);
        Assert.Equal(7, (await tarifario.ObtenerAsync(operadorB))!.Version);
        Assert.Equal(1, (await tarifario.ObtenerAsync(operadorA))!.Version);

        Assert.Equal(2, origen.Llamadas);
        Assert.StartsWith(TenantCacheKey.PrefijoOperador(operadorA), TarifarioVigenteCacheado.Clave(operadorA));
    }

    [Fact]
    public async Task InvalidarTarifarioObligaALeerLaVersionNueva()
    {
        var operador = Guid.NewGuid();
        var origen = new TarifarioFalso();
        origen.Cuadros[operador] = Cuadro(version: 1);
        var tarifario = Tarifario(origen);
        await tarifario.ObtenerAsync(operador);

        origen.Cuadros[operador] = Cuadro(version: 2);
        Assert.Equal(1, (await tarifario.ObtenerAsync(operador))!.Version);
        await tarifario.InvalidarAsync(operador);

        Assert.Equal(2, (await tarifario.ObtenerAsync(operador))!.Version);
        Assert.Equal(new ResumenCache("tarifario", 1, 2, 0, Math.Round(1 / 3d, 4)), _metricas.Resumen().Single());
    }

    [Fact]
    public async Task TarifarioVenceCuandoEntraEnVigorElSiguienteCuadro()
    {
        var conProxima = Guid.NewGuid();
        var sinProxima = Guid.NewGuid();
        var origen = new TarifarioFalso();
        origen.Cuadros[conProxima] = Cuadro(version: 1, proximaVigencia: Ahora.AddMinutes(5));
        origen.Cuadros[sinProxima] = Cuadro(version: 1);
        var tarifario = Tarifario(origen);

        await tarifario.ObtenerAsync(conProxima);
        await tarifario.ObtenerAsync(sinProxima);

        Assert.Equal(TimeSpan.FromMinutes(5), _cache.Duraciones[TarifarioVigenteCacheado.Clave(conProxima)]);
        Assert.Equal(_opciones.DuracionTarifario, _cache.Duraciones[TarifarioVigenteCacheado.Clave(sinProxima)]);
    }

    [Fact]
    public async Task SiLaCacheFallaSeLeeDeLaBaseYNoSeReintentaHastaQuePaseLaPausa()
    {
        _cache.Caida = true;
        var origen = new SeguimientoFalso();
        var seguimiento = Seguimiento(origen);
        var token = Guid.NewGuid().ToString("N");

        Assert.NotNull(await seguimiento.ConsultarAsync(token));
        Assert.NotNull(await seguimiento.ConsultarAsync(token));

        // Solo el primer intento tocó la caché; el segundo fue directo a la base.
        Assert.Equal(2, origen.Llamadas);
        Assert.Equal(1, _cache.Intentos);
        Assert.Equal(new ResumenCache("seguimiento", 0, 0, 1, 0), _metricas.Resumen().Single());

        _cache.Caida = false;
        _reloj.Avanzar(CacheSegura.PausaTrasFalla);
        await seguimiento.ConsultarAsync(token);
        await seguimiento.ConsultarAsync(token);

        Assert.Equal(3, origen.Llamadas);
        Assert.Equal(new ResumenCache("seguimiento", 1, 1, 1, 0.5), _metricas.Resumen().Single());
    }

    private CacheSegura NuevaCache() => new(_cache, _metricas, NullLogger<CacheSegura>.Instance, _reloj);

    private SeguimientoPublicoCacheado Seguimiento(ISeguimientoPublico origen) => new(origen, NuevaCache(), _opciones);

    private TarifarioVigenteCacheado Tarifario(ITarifarioVigente origen) => new(origen, NuevaCache(), _opciones, _reloj);

    private static TarifarioDto Cuadro(int version, DateTime? proximaVigencia = null) =>
        new(Guid.NewGuid(), version, Ahora.AddDays(-1), proximaVigencia,
            [new LineaTarifaDto(Guid.NewGuid(), ModalidadServicio.Estandar, 0, 10, 1, 150,
                [new AjusteTarifaDto(TipoAjuste.Recargo, 10)])]);

    private sealed class SeguimientoFalso : ISeguimientoPublico
    {
        public bool Existe { get; init; } = true;
        public int Llamadas { get; private set; }

        public Task<SeguimientoPublicoDto?> ConsultarAsync(string token, CancellationToken cancellationToken = default)
        {
            Llamadas++;
            return Task.FromResult(Existe
                ? new SeguimientoPublicoDto("Operador", EstadoEnvio.EnTransito, Ahora,
                    new DateTimeOffset(Ahora.AddHours(-1)), new DateTimeOffset(Ahora.AddHours(1)),
                    new AvanceBultosDto(3, 2, 0),
                    [new HitoSeguimientoDto(TipoEvento.Admitir, EstadoEnvio.Admitido, Ahora.AddDays(-1)),
                     new HitoSeguimientoDto(TipoEvento.IniciarEntrega, EstadoEnvio.EnTransito, Ahora.AddHours(-2))])
                : null);
        }
    }

    private sealed class TarifarioFalso : ITarifarioVigente
    {
        public Dictionary<Guid, TarifarioDto> Cuadros { get; } = [];
        public int Llamadas { get; private set; }

        public Task<TarifarioDto?> ObtenerAsync(Guid operadorId, CancellationToken cancellationToken = default)
        {
            Llamadas++;
            return Task.FromResult(Cuadros.GetValueOrDefault(operadorId));
        }
    }

    private sealed class RelojManual : TimeProvider
    {
        private DateTimeOffset _ahora = Ahora;
        public override DateTimeOffset GetUtcNow() => _ahora;
        public void Avanzar(TimeSpan tiempo) => _ahora += tiempo;
    }

    // Caché en memoria que además anota la duración de cada entrada y puede simular que Redis está caído.
    private sealed class CacheEspia : IDistributedCache
    {
        private readonly MemoryDistributedCache _memoria = new(Options.Create(new MemoryDistributedCacheOptions()));
        public bool Caida { get; set; }
        public int Intentos { get; private set; }
        public Dictionary<string, TimeSpan?> Duraciones { get; } = [];

        public byte[]? Get(string key) => Revisar(() => _memoria.Get(key));
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Revisar(() => _memoria.GetAsync(key, token));
        public void Refresh(string key) => _memoria.Refresh(key);
        public Task RefreshAsync(string key, CancellationToken token = default) => _memoria.RefreshAsync(key, token);
        public void Remove(string key) => _memoria.Remove(key);
        public Task RemoveAsync(string key, CancellationToken token = default) => Revisar(() => _memoria.RemoveAsync(key, token));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            SetAsync(key, value, options).GetAwaiter().GetResult();

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            Revisar(() =>
            {
                Duraciones[key] = options.AbsoluteExpirationRelativeToNow;
                return _memoria.SetAsync(key, value, options, token);
            });

        private T Revisar<T>(Func<T> accion)
        {
            Intentos++;
            return Caida ? throw new InvalidOperationException("Redis no responde") : accion();
        }
    }
}
