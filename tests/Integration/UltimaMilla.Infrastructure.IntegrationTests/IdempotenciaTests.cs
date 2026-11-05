using System.Collections.Concurrent;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Inbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private RegistroIdempotencia Registro() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_testConnection).Options, new RelojPublicacion());

    private static TenantContext TenantDe(EscenarioPersistencia e) =>
        TenantContext.ParaMensaje(e.Operador.Id, e.Envio.CuentaComercial.Id); // con cuenta, como llega del Worker

    [PostgresFact]
    public async Task IdempotenciaDescartaDuplicadosYUnaAccionFallidaNoConsumeLaClave()
    {
        var e = await GuardarEscenario();
        var registro = Registro();
        var tenant = TenantDe(e);
        var mensaje = Guid.NewGuid();
        var ejecuciones = 0;
        Task Accion(CancellationToken _) { ejecuciones++; return Task.CompletedTask; }

        // La acción falla: nada queda marcado, y el reintento sí se ejecuta.
        await Assert.ThrowsAsync<IOException>(() => registro.EjecutarUnaVezAsync(tenant, mensaje, "avisos",
            _ => throw new IOException("falla")));
        await using (var lector = Context())
            Assert.Equal(0, await lector.Set<MensajeProcesado>().CountAsync());

        Assert.True(await registro.EjecutarUnaVezAsync(tenant, mensaje, "avisos", Accion));
        Assert.False(await registro.EjecutarUnaVezAsync(tenant, mensaje, "avisos", Accion)); // duplicado
        Assert.False(await registro.EjecutarUnaVezAsync(tenant, mensaje, "avisos", Accion));
        Assert.Equal(1, ejecuciones);

        // La clave es (MensajeId, Consumidor): otro consumidor sí lo procesa.
        Assert.True(await registro.EjecutarUnaVezAsync(tenant, mensaje, "otro-consumidor", Accion));
        Assert.Equal(2, ejecuciones);

        await using var final = Context();
        var filas = await final.Set<MensajeProcesado>().AsNoTracking().ToListAsync();
        Assert.Equal(2, filas.Count);
        Assert.All(filas, f => Assert.Equal(mensaje, f.MensajeId));
        Assert.All(filas, f => Assert.Equal(EscenarioPersistencia.Fecha.AddDays(20), f.ProcesadoEn));
        Assert.All(await final.Set<MensajeProcesado>().Select(f => EF.Property<Guid>(f, "TenantId")).ToListAsync(),
            t => Assert.Equal(e.Operador.Id, t));
    }

    [PostgresFact]
    public async Task IdempotenciaConcurrenteEjecutaLaAccionUnaSolaVez()
    {
        var e = await GuardarEscenario();
        var registro = Registro();
        var tenant = TenantDe(e);
        var mensaje = Guid.NewGuid();
        var ejecuciones = 0;

        var resultados = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => registro.EjecutarUnaVezAsync(tenant, mensaje, "avisos",
            async ct => { Interlocked.Increment(ref ejecuciones); await Task.Delay(150, ct); })));

        Assert.Equal(1, resultados.Count(r => r));
        Assert.Equal(1, ejecuciones);
        await using var lector = Context();
        Assert.Equal(1, await lector.Set<MensajeProcesado>().CountAsync());
    }

    [PostgresFact]
    public async Task LosRegistrosDeIdempotenciaQuedanAisladosPorOperador()
    {
        var a = await GuardarEscenario();
        var b = await GuardarEscenario();
        var registro = Registro();
        var mensaje = Guid.NewGuid();
        Assert.True(await registro.EjecutarUnaVezAsync(TenantDe(a), mensaje, "avisos", _ => Task.CompletedTask));

        await using var delA = Context(a.Operador.Id);
        await using var delB = Context(b.Operador.Id);
        Assert.Equal(1, await delA.Set<MensajeProcesado>().CountAsync());
        Assert.Equal(0, await delB.Set<MensajeProcesado>().CountAsync()); // B no ve lo de A
    }
}
