using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private PublicadorOutbox Publicador(AppDbContext db, ITransporteMensajes transporte,
        OpcionesPublicacionOutbox? opciones = null) => new(db, transporte, new RelojPublicacion(), opciones ?? new());

    private sealed class RelojPublicacion : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(EscenarioPersistencia.Fecha.AddDays(20));
    }

    private sealed class TransportePrueba(Func<MensajeParaPublicar, CancellationToken, Task> publicar) : ITransporteMensajes
    {
        public Task PublicarConfirmadoAsync(MensajeParaPublicar mensaje, CancellationToken token = default) => publicar(mensaje, token);
    }

    [PostgresFact]
    public async Task PublicadorRespetaLimiteYMarcaSoloDespuesDeConfirmar()
    {
        var e = await GuardarEscenario(recibido: true);
        var enviados = new List<Guid>();
        var transporte = new TransportePrueba(async (mensaje, token) =>
        {
            await using var reader = Context();
            Assert.Null((await reader.Set<MensajeOutbox>().SingleAsync(m => m.Id == mensaje.MensajeId, token)).PublicadoEn);
            Assert.Equal(e.Operador.Id, mensaje.OperadorId);
            Assert.Equal("envio.evento.v1", mensaje.Tipo);
            Assert.False(string.IsNullOrWhiteSpace(mensaje.Contenido));
            enviados.Add(mensaje.MensajeId);
        });
        await using var writer = Context();
        var publicador = Publicador(writer, transporte);
        Assert.Equal(1, await publicador.PublicarLoteAsync(1));
        await using (var reader = Context())
            Assert.Equal(1, await reader.Set<MensajeOutbox>().CountAsync(m => m.PublicadoEn != null));
        Assert.Equal(1, await publicador.PublicarLoteAsync());
        Assert.Equal(0, await publicador.PublicarLoteAsync());
        Assert.Equal(2, enviados.Distinct().Count());
        await using var final = Context();
        Assert.All(await final.Set<MensajeOutbox>().ToListAsync(), m => Assert.Equal(EscenarioPersistencia.Fecha.AddDays(20), m.PublicadoEn));
        Assert.Equal(EstadoEnvio.EnDeposito, (await final.Envios.SingleAsync()).Estado);
    }

    [PostgresFact]
    public async Task PublicadorPropagaFalloYUnaNuevaEjecucionConservaElIdentificador()
    {
        await GuardarEscenario();
        Guid fallido = Guid.Empty;
        await using (var writer = Context())
        {
            var transporte = new TransportePrueba((mensaje, _) =>
            {
                fallido = mensaje.MensajeId;
                throw new IOException("Sin confirmación del transporte de prueba");
            });
            await Assert.ThrowsAsync<IOException>(() => Publicador(writer, transporte).PublicarLoteAsync());
            Assert.Empty(writer.ChangeTracker.Entries());
        }
        await using (var reader = Context())
        {
            var mensaje = await reader.Set<MensajeOutbox>().SingleAsync();
            Assert.Null(mensaje.PublicadoEn);
            Assert.Null(mensaje.CuarentenaEn);
            Assert.Equal(0, mensaje.IntentosFallidos);
        }
        await using var next = Context();
        Assert.Equal(1, await Publicador(next, new TransportePrueba((mensaje, _) =>
        {
            Assert.Equal(fallido, mensaje.MensajeId);
            return Task.CompletedTask;
        })).PublicarLoteAsync());
    }

    [PostgresFact]
    public async Task PublicadorApartaElMensajeTrasTresFallosYPublicaElSiguienteDelMismoOperador()
    {
        var escenario = await GuardarEscenario(recibido: true);
        await using var lectura = Context();
        var ids = await lectura.Set<MensajeOutbox>().OrderBy(m => m.CreadoEn).ThenBy(m => m.Id)
            .Select(m => m.Id).ToArrayAsync();
        var enviados = new List<Guid>();
        var servicio = new PublicacionOutboxPorOperador(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_testConnection).Options,
            new TransportePrueba((mensaje, _) =>
            {
                Assert.Equal(escenario.Operador.Id, mensaje.OperadorId);
                enviados.Add(mensaje.MensajeId);
                return mensaje.MensajeId == ids[0]
                    ? throw new MensajeNoPublicableException("Mensaje sin ruta en el transporte de prueba.")
                    : Task.CompletedTask;
            }), new RelojPublicacion(), new OpcionesPublicacionOutbox());
        var operador = escenario.Operador.Id;

        for (var intento = 1; intento <= 2; intento++)
        {
            await Assert.ThrowsAsync<MensajeNoPublicableException>(() => servicio.PublicarLoteAsync(operador));
            var pendiente = await lectura.Set<MensajeOutbox>().AsNoTracking().SingleAsync(m => m.Id == ids[0]);
            Assert.Equal(intento, pendiente.IntentosFallidos);
            Assert.Null(pendiente.CuarentenaEn);
            Assert.Null(pendiente.PublicadoEn);
        }

        Assert.Equal(1, await servicio.PublicarLoteAsync(operador));
        Assert.Equal(new[] { ids[0], ids[0], ids[0], ids[1] }, enviados);
        var mensajes = await lectura.Set<MensajeOutbox>().ToDictionaryAsync(m => m.Id);
        Assert.Equal(3, mensajes[ids[0]].IntentosFallidos);
        Assert.Equal(EscenarioPersistencia.Fecha.AddDays(20), mensajes[ids[0]].CuarentenaEn);
        Assert.Null(mensajes[ids[0]].PublicadoEn);
        Assert.NotNull(mensajes[ids[1]].PublicadoEn);
        Assert.Null(mensajes[ids[1]].CuarentenaEn);
        Assert.Empty(await servicio.ListarConPendientesAsync());
        Assert.Equal(0, await servicio.PublicarLoteAsync(operador));
        Assert.Equal(4, enviados.Count);
    }

    [PostgresFact]
    public async Task PublicadorConservaConfirmadosPreviosSiElSegundoMensajeFalla()
    {
        await GuardarEscenario(recibido: true);
        var llamadas = 0;
        await using var writer = Context();
        var transporte = new TransportePrueba((_, _) => ++llamadas == 1
            ? Task.CompletedTask : throw new IOException("Falló el segundo"));
        await Assert.ThrowsAsync<IOException>(() => Publicador(writer, transporte).PublicarLoteAsync());
        Assert.Equal(2, llamadas);
        await using var reader = Context();
        Assert.Equal(1, await reader.Set<MensajeOutbox>().CountAsync(m => m.PublicadoEn != null));
        Assert.Equal(1, await reader.Set<MensajeOutbox>().CountAsync(m => m.PublicadoEn == null));
    }

    [PostgresFact]
    public async Task PublicadoresConcurrentesOmitenLaFilaBloqueadaSinDuplicarla()
    {
        await GuardarEscenario(recibido: true);
        var tomado = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var liberar = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enviados = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        await using var first = Context();
        await using var second = Context();
        var primero = Publicador(first, new TransportePrueba(async (mensaje, token) =>
        {
            enviados.Add(mensaje.MensajeId);
            tomado.SetResult(mensaje.MensajeId);
            await liberar.Task.WaitAsync(token);
        })).PublicarLoteAsync(1);
        try
        {
            var bloqueado = await tomado.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(1, await Publicador(second, new TransportePrueba((mensaje, _) =>
            {
                Assert.NotEqual(bloqueado, mensaje.MensajeId);
                enviados.Add(mensaje.MensajeId);
                return Task.CompletedTask;
            })).PublicarLoteAsync(1).WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally { liberar.TrySetResult(); await primero; }
        Assert.Equal(2, enviados.Distinct().Count());
        await using var reader = Context();
        Assert.Equal(2, await reader.Set<MensajeOutbox>().CountAsync(m => m.PublicadoEn != null));
    }

    [PostgresFact]
    public async Task PublicadorCanceladoLiberaBloqueoYDejaMensajePendiente()
    {
        await GuardarEscenario();
        var tomado = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancelar = new CancellationTokenSource();
        await using var writer = Context();
        var ejecucion = Publicador(writer, new TransportePrueba(async (_, token) =>
        {
            tomado.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        })).PublicarLoteAsync(cancellationToken: cancelar.Token);
        try { await tomado.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { cancelar.Cancel(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ejecucion);
        await using var next = Context();
        var pendiente = await next.Set<MensajeOutbox>().AsNoTracking().SingleAsync();
        Assert.Null(pendiente.PublicadoEn);
        Assert.Equal(0, pendiente.IntentosFallidos);
        Assert.Equal(1, await Publicador(next, new TransportePrueba((_, _) => Task.CompletedTask)).PublicarLoteAsync());
    }

    [PostgresFact]
    public async Task PublicadorLimitaEsperaDeConfirmacionYNoMarcaElMensaje()
    {
        await GuardarEscenario();
        await using var writer = Context();
        var transporte = new TransportePrueba((_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Publicador(writer, transporte,
            new() { TiempoMaximoConfirmacion = TimeSpan.FromMilliseconds(50) }).PublicarLoteAsync());
        await using var reader = Context();
        var pendiente = await reader.Set<MensajeOutbox>().SingleAsync();
        Assert.Null(pendiente.PublicadoEn);
        Assert.Equal(0, pendiente.IntentosFallidos);
    }

    [PostgresFact]
    public async Task PublicadorNoProcesaMensajesDeOtroOperador()
    {
        var (a, b) = await DosOperadores();
        await using var writer = Context(a.Operador.Id);
        Assert.Equal(1, await Publicador(writer, new TransportePrueba((mensaje, _) =>
        {
            Assert.Equal(a.Operador.Id, mensaje.OperadorId);
            Assert.Equal(a.Envio.Eventos.Single().Id, mensaje.MensajeId);
            return Task.CompletedTask;
        })).PublicarLoteAsync());
        await using var other = Context(b.Operador.Id);
        Assert.Null((await other.Set<MensajeOutbox>().SingleAsync()).PublicadoEn);
    }

    [PostgresFact]
    public async Task PublicadorFalloAlMarcarNoPierdeMensajeYaConfirmadoEnTransporte()
    {
        await GuardarEscenario();
        var enviados = new List<Guid>();
        var transporte = new TransportePrueba((mensaje, _) => { enviados.Add(mensaje.MensajeId); return Task.CompletedTask; });
        await using (var writer = Context())
        {
            await writer.Database.ExecuteSqlRawAsync("""
                ALTER TABLE "MensajesOutbox" ADD CONSTRAINT "FallaMarcadoPrueba" CHECK ("PublicadoEn" IS NULL)
                """);
            await Assert.ThrowsAsync<DbUpdateException>(() => Publicador(writer, transporte).PublicarLoteAsync());
        }
        await using (var setup = Context())
        {
            var pendiente = await setup.Set<MensajeOutbox>().AsNoTracking().SingleAsync();
            Assert.Null(pendiente.PublicadoEn);
            Assert.Equal(0, pendiente.IntentosFallidos);
            await setup.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" DROP CONSTRAINT \"FallaMarcadoPrueba\"");
        }
        await using var next = Context();
        Assert.Equal(1, await Publicador(next, transporte).PublicarLoteAsync());
        Assert.Equal(2, enviados.Count);
        Assert.Equal(enviados[0], enviados[1]); // Duplicado identificable, no entrega exactamente una vez.
    }

    [PostgresFact]
    public async Task PublicadorRechazaContextoConNegocioCargadoOTransaccionPrevia()
    {
        await GuardarEscenario();
        var transporte = new TransportePrueba((_, _) => throw new Xunit.Sdk.XunitException("No debe invocar al transporte"));
        await using (var writer = Context())
        {
            _ = await writer.Envios.SingleAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => Publicador(writer, transporte).PublicarLoteAsync());
        }
        await using (var writer = Context())
        {
            await using var transaction = await writer.Database.BeginTransactionAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => Publicador(writer, transporte).PublicarLoteAsync());
        }
    }
}
