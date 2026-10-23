using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Infrastructure.Cache;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task TarifarioVigenteLeeElCuadroDelOperadorYSuProximaVigencia()
    {
        var escenario = Escenario();
        var futuro = DateTime.UtcNow.AddDays(10);
        await using (var writer = Context())
        {
            writer.Add(escenario.Envio);
            writer.Add(new CuadroTarifario(escenario.Operador, 2, futuro));
            await writer.SaveChangesAsync();
        }
        var tarifario = new TarifarioVigente(Options(), TimeProvider.System);

        var vigente = await tarifario.ObtenerAsync(escenario.Operador.Id);

        Assert.NotNull(vigente);
        Assert.Equal(1, vigente.Version);
        Assert.Equal(futuro, vigente.ProximaVigencia!.Value, TimeSpan.FromMilliseconds(1));
        var linea = Assert.Single(vigente.Lineas);
        Assert.Equal(escenario.Zona.Id, linea.ZonaId);
        Assert.Equal(150, linea.Precio);
        Assert.Equal(TipoAjuste.Recargo, Assert.Single(linea.Ajustes).Tipo);
        // Otro operador no ve este tarifario aunque comparta la base.
        Assert.Null(await tarifario.ObtenerAsync(Guid.NewGuid()));
    }

    [PostgresFact]
    public async Task SeguimientoCacheadoConsultaLaBaseUnaSolaVez()
    {
        var escenario = Escenario();
        await using (var writer = Context())
        {
            writer.Add(escenario.Envio);
            await writer.SaveChangesAsync();
        }
        var metricas = new CacheMetricas();
        var cache = new CacheSegura(new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())),
            metricas, NullLogger<CacheSegura>.Instance, TimeProvider.System);
        var seguimiento = new SeguimientoPublicoCacheado(new SeguimientoPublico(Options()), cache, new CacheOpciones());

        var primero = await seguimiento.ConsultarAsync(escenario.Envio.CodigoSeguimiento);
        var segundo = await seguimiento.ConsultarAsync(escenario.Envio.CodigoSeguimiento);

        Assert.NotNull(primero);
        Assert.Equal(EstadoEnvio.Admitido, primero.Estado);
        Assert.Equal(primero, segundo);
        Assert.Equal(new ResumenCache("seguimiento", 1, 1, 0, 0.5), metricas.Resumen().Single());
    }
}
