using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task EnumeraPorOperadorSoloLosQueTienenPendientesYPublicaSinMezclarlos()
    {
        var a = await GuardarEscenario(); var operadorA = a.Operador.Id;
        var b = await GuardarEscenario(); var operadorB = b.Operador.Id;
        Assert.NotEqual(operadorA, operadorB);

        var vistos = new List<(Guid Operador, Guid Mensaje)>();
        var servicio = new PublicacionOutboxPorOperador(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_testConnection).Options,
            new TransportePrueba((m, _) => { vistos.Add((m.OperadorId, m.MensajeId)); return Task.CompletedTask; }),
            new RelojPublicacion(), new OpcionesPublicacionOutbox());

        Assert.Equal(new[] { operadorA, operadorB }.Order(), (await servicio.ListarConPendientesAsync()).Order());

        Assert.Equal(1, await servicio.PublicarLoteAsync(operadorA));
        Assert.All(vistos, v => Assert.Equal(operadorA, v.Operador));
        Assert.Equal([operadorB], await servicio.ListarConPendientesAsync());

        Assert.Equal(1, await servicio.PublicarLoteAsync(operadorB));
        Assert.Empty(await servicio.ListarConPendientesAsync());
        Assert.Equal(0, await servicio.PublicarLoteAsync(operadorB));
    }
}
