using System.Security.Claims;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.RegistrarIntento;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Intentos;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private RegistrarIntentoHandler RegistroHandler(TenantContext tenant) => new(
        new RegistroIntentoRepository(Options(), new TenantActual(tenant)));

    private async Task<TenantContext> IdentidadRepartidor(EscenarioPersistencia e, Guid? usuarioId = null) => await Resolver().DesdeClaimsAsync(
        new ClaimsPrincipal(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, (usuarioId ?? e.Repartidor.Id).ToString()), new("operador_id", e.Operador.Id.ToString()),
            new(ClaimTypes.Role, RolUsuario.Repartidor.ToString())], "prueba-autenticada")));

    private async Task<RegistrarIntentoCommand> ComandoRegistro(EscenarioPersistencia e)
    {
        await using var db = Context(e.Operador.Id);
        var envio = await EscenarioPersistencia.Grafo(db).SingleAsync(x => x.Id == e.Envio.Id);
        var datos = SolicitudParcial(envio);
        var pruebas = datos.SelectMany(d => d.Pruebas).DistinctBy(p => p.Id).ToArray();
        return new(envio.Id, envio.Parada!.Id, db.Entry(envio).Property<Guid>("RowVersion").CurrentValue,
            Guid.NewGuid(), EscenarioPersistencia.Fecha.AddHours(1),
            datos.Select(d => new ResultadoBultoCommand(d.BultoId, d.Resultado, d.Motivo?.Id, d.Pruebas.Select(p => p.Id).ToArray())).ToArray(),
            pruebas.Select(p => new EvidenciaIntentoCommand(p.Id, p.TipoPrueba, p.Hora, p.Posicion, p.ArchivoRef?.Ubicacion, p.ValorTexto)).ToArray());
    }

    [PostgresFact]
    public async Task CasoDeUsoResuelveIdentidadRegistraParcialYRepiteConVersionOriginal()
    {
        var e = await PrepararVisitaParcial();
        var command = await ComandoRegistro(e);
        var handler = RegistroHandler(await IdentidadRepartidor(e));
        var primero = await handler.Handle(command);
        Assert.False(primero.Repetido);
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, primero.EstadoActualEnvio);
        Assert.NotEqual(command.VersionEsperada, primero.VersionActualEnvio);
        var segundo = await handler.Handle(command);
        Assert.True(segundo.Repetido);
        Assert.Equal(primero.IntentoId, segundo.IntentoId);
        Assert.Equal(primero.VersionActualEnvio, segundo.VersionActualEnvio);
        await using var reader = Context(e.Operador.Id);
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(primero.VersionActualEnvio, reader.Entry(actual).Property<Guid>("RowVersion").CurrentValue);
        Assert.Equal(e.Repartidor.Id, actual.Eventos.Last(v => v.IdOperacion == command.IdOperacion).ResponsableId);
        Assert.Equal(3, actual.Intentos.Single().Detalles.Count);
        Assert.Equal(2, actual.Intentos.Single().Pruebas.Count);
        Assert.Equal(5, await reader.Set<MensajeOutbox>().CountAsync());
        Assert.Single(await reader.Set<MensajeOutbox>().Where(m => m.Tipo == "envio.evento.v2").ToListAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command with
        { Evidencias = command.Evidencias.Select((p, i) => i == 0 ? p with { Archivo = "otra.jpg" } : p).ToArray() }));
        Assert.Equal(5, await reader.Set<MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task CasoDeUsoRechazaVersionViejaSinAlterarDatos()
    {
        var e = await PrepararVisitaParcial();
        var command = await ComandoRegistro(e);
        var handler = RegistroHandler(await IdentidadRepartidor(e));
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => handler.Handle(command with { VersionEsperada = Guid.NewGuid() }));
        await using var reader = Context(e.Operador.Id);
        var envio = await reader.Envios.Include(x => x.Bultos).SingleAsync();
        Assert.Equal(command.VersionEsperada, reader.Entry(envio).Property<Guid>("RowVersion").CurrentValue);
        Assert.Equal(EstadoEnvio.EnTransito, envio.Estado);
        Assert.All(envio.Bultos, b => Assert.Equal(EstadoBulto.EnTransito, b.Estado));
        Assert.Empty(await reader.Set<IntentoEntrega>().ToListAsync());
        Assert.Equal(4, await reader.Set<MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task CasoDeUsoExigeRepartidorAutenticadoActivoYAsignadoSinCruzarOperador()
    {
        var e = await PrepararVisitaParcial();
        var command = await ComandoRegistro(e);
        var otro = await GuardarEscenario();
        var contextos = new[]
        {
            TenantContext.ParaProcesoInterno(e.Operador.Id), TenantContext.ParaMensaje(e.Operador.Id),
            TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador),
            TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Repartidor),
            TenantContext.ParaUsuario(e.Operador.Id, otro.Repartidor.Id, RolUsuario.Repartidor),
            await IdentidadRepartidor(otro)
        };
        foreach (var tenant in contextos)
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => RegistroHandler(tenant).Handle(command));

        await using (var db = Context(e.Operador.Id))
        {
            var op = await db.Set<Operador>().SingleAsync();
            var noAsignado = new Repartidor(op, "no-asignado@example.com");
            db.Add(noAsignado); await db.SaveChangesAsync();
            var tenant = TenantContext.ParaUsuario(e.Operador.Id, noAsignado.Id, RolUsuario.Repartidor);
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => RegistroHandler(tenant).Handle(command));
        }
        var identidad = await IdentidadRepartidor(e);
        await using var operacion = await new RegistroIntentoRepository(Options(), new TenantActual(identidad))
            .AbrirAsync(command.EnvioId, command.ParadaId, command.IdOperacion, CancellationToken.None);
        await using (var db = Context(e.Operador.Id))
        {
            var usuario = await db.Set<Repartidor>().SingleAsync(u => u.Id == e.Repartidor.Id);
            db.Entry(usuario).Property(u => u.Activo).CurrentValue = false;
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => RegistroHandler(identidad).Handle(command));
        operacion.Envio.RegistrarIntentoPorBulto(command.ParadaId, SolicitudParcial(operacion.Envio),
            operacion.Responsable, command.OcurridoEn, command.IdOperacion);
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => operacion.GuardarAsync(CancellationToken.None));
        await using var reader = Context(e.Operador.Id);
        Assert.Empty(await reader.Set<IntentoEntrega>().ToListAsync());
        Assert.Equal(4, await reader.Set<MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task CasoDeUsoRevierteFalloDeOutboxYUnaNuevaEjecucionPuedeContinuar()
    {
        var e = await PrepararVisitaParcial();
        var command = await ComandoRegistro(e);
        var handler = RegistroHandler(await IdentidadRepartidor(e));
        await using var control = Context(e.Operador.Id);
        await control.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" ADD CONSTRAINT \"FalloCasoUso\" CHECK (\"Tipo\" <> 'envio.evento.v2') NOT VALID");
        await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(command));
        Assert.Equal(EstadoEnvio.EnTransito, await control.Envios.Select(x => x.Estado).SingleAsync());
        Assert.Equal(command.VersionEsperada, await control.Envios.Select(x => EF.Property<Guid>(x, "RowVersion")).SingleAsync());
        Assert.Empty(await control.Set<DetalleIntentoBulto>().ToListAsync());
        Assert.Empty(await control.Set<PruebaBulto>().ToListAsync());
        Assert.Equal(4, await control.Set<MensajeOutbox>().CountAsync());
        await control.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" DROP CONSTRAINT \"FalloCasoUso\"");
        Assert.False((await handler.Handle(command)).Repetido);
        Assert.Equal(5, await control.Set<MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task DosCasosDeUsoConLaMismaVersionSoloConfirmanUno()
    {
        var e = await PrepararVisitaParcial();
        var command = await ComandoRegistro(e);
        var repository = new RegistroIntentoRepository(Options(), new TenantActual(await IdentidadRepartidor(e)));
        var barrera = new RepositoryConBarrera(repository);
        var handler = new RegistrarIntentoHandler(barrera);
        var resultados = await Task.WhenAll(Registrar(command), Registrar(command with { IdOperacion = Guid.NewGuid() }));
        Assert.Single(resultados, x => x);
        Assert.Single(resultados, x => !x);
        await using var db = Context(e.Operador.Id);
        Assert.Single(await db.Set<IntentoEntrega>().ToListAsync());
        Assert.Equal(3, await db.Set<DetalleIntentoBulto>().CountAsync());
        Assert.Equal(5, await db.Set<MensajeOutbox>().CountAsync());

        async Task<bool> Registrar(RegistrarIntentoCommand solicitud)
        {
            try { await handler.Handle(solicitud); return true; }
            catch (ConflictoConcurrenciaException) { return false; }
        }
    }

    private sealed class RepositoryConBarrera(IRegistroIntentoRepository inner) : IRegistroIntentoRepository
    {
        private int _llegadas;
        private readonly TaskCompletionSource _ambos = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IOperacionRegistroIntento> AbrirAsync(Guid envioId, Guid paradaId, Guid idOperacion, CancellationToken ct)
        {
            var operacion = await inner.AbrirAsync(envioId, paradaId, idOperacion, ct);
            if (Interlocked.Increment(ref _llegadas) == 2) _ambos.TrySetResult();
            try { await _ambos.Task.WaitAsync(TimeSpan.FromSeconds(20), ct); return operacion; }
            catch { await operacion.DisposeAsync(); throw; }
        }
    }
}
