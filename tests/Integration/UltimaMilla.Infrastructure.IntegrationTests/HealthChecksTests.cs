using System.Net;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task HealthChecksDistinguenProcesoVivoDePostgreSqlDisponible()
    {
        using var servidor = new ServidorApi(_testConnection, usarIdentidadPrueba: false);
        using var cliente = servidor.Cliente();
        foreach (var ruta in new[] { "/live", "/ready", "/health" })
        {
            using var respuesta = await cliente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.Equal("Healthy", await respuesta.Content.ReadAsStringAsync());
            Assert.True(respuesta.Headers.CacheControl?.NoStore);
        }

        // Mismo servidor PostgreSQL, pero una base inexistente fuerza un fallo real de conexión.
        var conexionFallida = new NpgsqlConnectionStringBuilder(_testConnection)
        {
            Database = "inexistente_" + Guid.NewGuid().ToString("N"),
            Pooling = false
        };
        using var sinBase = new ServidorApi(conexionFallida.ConnectionString, usarIdentidadPrueba: false);
        using var clienteSinBase = sinBase.Cliente();
        foreach (var ruta in new[] { "/ready", "/health" })
        {
            using var respuesta = await clienteSinBase.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
            Assert.Equal("Unhealthy", await respuesta.Content.ReadAsStringAsync());
            Assert.True(respuesta.Headers.CacheControl?.NoStore);
        }
        using var vivo = await clienteSinBase.GetAsync("/live");
        Assert.Equal(HttpStatusCode.OK, vivo.StatusCode);
        Assert.Equal("Healthy", await vivo.Content.ReadAsStringAsync());
    }
}
