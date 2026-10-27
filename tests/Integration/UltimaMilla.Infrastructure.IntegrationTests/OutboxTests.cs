using System.Text.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task MigracionNoPublicaHistorialPrevioYLosEventosNuevosSiGeneranMensaje()
    {
        await GuardarEscenario(recibido: true);
        await using (var migration = Context())
        {
            var migrator = migration.GetService<IMigrator>();
            await migrator.MigrateAsync("20261021210217_OptimisticConcurrency");
            await migrator.MigrateAsync();
        }
        await using (var writer = Context())
        {
            Assert.Empty(await writer.Set<MensajeOutbox>().ToListAsync());
            var envio = await EscenarioPersistencia.Grafo(writer).SingleAsync();
            Assert.Equal(2, envio.Eventos.Count);
            envio.MarcarExtraviado(envio.Eventos.First().Responsable, EscenarioPersistencia.Fecha, "Nuevo evento");
            await writer.SaveChangesAsync();
        }
        await using var reader = Context();
        Assert.Equal(3, await reader.Set<EventoDeEnvio>().CountAsync());
        Assert.Equal("EventoAnomalo", JsonSerializer.Deserialize<EventoEnvioV1>(
            (await reader.Set<MensajeOutbox>().SingleAsync()).Contenido,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.TipoEvento);
    }

    [PostgresFact]
    public async Task AltaGuardaMensajeVersionadoConAmbitoYSinDatosDelDestinatario()
    {
        var e = await GuardarEscenario();
        await using var reader = Context();
        var mensaje = await reader.Set<MensajeOutbox>().SingleAsync();
        var payload = JsonSerializer.Deserialize<EventoEnvioV1>(mensaje.Contenido, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(e.Envio.Eventos.Single().Id, mensaje.Id);
        Assert.Equal(mensaje.Id, payload.MensajeId);
        Assert.Equal(payload.EventoId, payload.MensajeId);
        Assert.Equal(1, payload.Version);
        Assert.Equal("envio.evento.v1", mensaje.Tipo);
        Assert.Equal(e.Operador.Id, payload.OperadorId);
        Assert.Equal(e.Envio.CuentaComercialId, payload.CuentaComercialId);
        Assert.Equal(e.Envio.Id, payload.EnvioId);
        Assert.Equal("Admitir", payload.TipoEvento);
        Assert.Null(payload.EstadoAnterior);
        Assert.Equal("Admitido", payload.EstadoNuevo);
        Assert.Equal(EscenarioPersistencia.Fecha, payload.OcurridoEn);
        Assert.Null(mensaje.PublicadoEn);
        Assert.Equal(DateTimeKind.Utc, mensaje.CreadoEn.Kind);
        Assert.Equal(e.Operador.Id, reader.Entry(mensaje).Property<Guid>("TenantId").CurrentValue);
        Assert.DoesNotContain("persona@example.com", mensaje.Contenido);
        Assert.DoesNotContain(e.Envio.CodigoSeguimiento, mensaje.Contenido);
    }

    [PostgresFact]
    public async Task GuardadoSincronoYRelecturaNoDuplicanMensajes()
    {
        var e = Escenario();
        using (var writer = Context())
        {
            writer.Add(e.Envio);
            writer.SaveChanges();
            e.Recibir();
            writer.SaveChanges();
            Assert.Equal(0, writer.SaveChanges());
        }
        await using var reader = Context();
        var envio = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(0, await reader.SaveChangesAsync());
        var mensajes = await reader.Set<MensajeOutbox>().OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(envio.Eventos.Select(v => v.Id).Order(), mensajes.Select(m => m.Id));
    }

    [PostgresFact]
    public async Task FallaDelInsertOutboxRevierteEstadoEventoYVersion()
    {
        await GuardarEscenario(recibido: true);
        await using var writer = Context();
        var envio = await EscenarioPersistencia.Grafo(writer).SingleAsync();
        var version = writer.Entry(envio).Property<Guid>("RowVersion").CurrentValue;
        // Falla real de PostgreSQL al insertar el mensaje nuevo, después de guardar el alta.
        await writer.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "MensajesOutbox" ADD CONSTRAINT "FallaOutboxPrueba"
            CHECK ("Tipo" <> 'envio.evento.v1') NOT VALID
            """);
        envio.MarcarExtraviado(envio.Eventos.First().Responsable, EscenarioPersistencia.Fecha, "Extraviado");
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => writer.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        await using var reader = Context();
        var actual = await reader.Envios.SingleAsync();
        Assert.Equal(EstadoEnvio.EnDeposito, actual.Estado);
        Assert.Equal(version, reader.Entry(actual).Property<Guid>("RowVersion").CurrentValue);
        Assert.Equal(2, await reader.Set<EventoDeEnvio>().CountAsync());
        Assert.Equal(2, await reader.Set<MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task ConflictoDeConcurrenciaNoDejaMensajeDeLaOperacionPerdedora()
    {
        await GuardarEscenario(recibido: true);
        await using var first = Context();
        await using var second = Context();
        var a = await EscenarioPersistencia.Grafo(first).SingleAsync();
        var b = await EscenarioPersistencia.Grafo(second).SingleAsync();
        a.MarcarExtraviado(a.Eventos.First().Responsable, EscenarioPersistencia.Fecha, "Primero");
        b.MarcarExtraviado(b.Eventos.First().Responsable, EscenarioPersistencia.Fecha, "Segundo");
        var loserId = b.Eventos.Last().Id;
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => second.SaveChangesAsync());
        await using var reader = Context();
        Assert.False(await reader.Set<MensajeOutbox>().AnyAsync(m => m.Id == loserId));
        Assert.Equal(3, await reader.Set<MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task RollbackExplicitoRevierteTambienElMensaje()
    {
        var e = Escenario();
        await using (var writer = Context())
        {
            await using var transaction = await writer.Database.BeginTransactionAsync();
            writer.Add(e.Envio);
            await writer.SaveChangesAsync();
            Assert.Single(await writer.Set<MensajeOutbox>().ToListAsync());
            await transaction.RollbackAsync();
        }
        await using var reader = Context();
        Assert.Empty(await reader.Envios.ToListAsync());
        Assert.Empty(await reader.Set<MensajeOutbox>().ToListAsync());
    }

    [PostgresFact]
    public async Task OutboxRespetaOperadorCuentaYContextoPublico()
    {
        var (a, b, ua, _) = await DosCuentas();
        var other = new EscenarioPersistencia();
        await using (var setup = Context(other.Operador.Id))
        {
            setup.Add(other.Envio);
            await setup.SaveChangesAsync();
        }
        await using (var db = Scoped(TenantContext.ParaUsuario(a.Operador.Id, ua.Id, RolUsuario.Comercio, a.Envio.CuentaComercialId)))
        {
            Assert.Equal(a.Envio.Eventos.Single().Id, (await db.Set<MensajeOutbox>().SingleAsync()).Id);
            Assert.Null(await db.Set<MensajeOutbox>().SingleOrDefaultAsync(m => m.Id == b.Eventos.Single().Id));
        }
        await using (var db = Context(a.Operador.Id))
            Assert.Equal(2, await db.Set<MensajeOutbox>().CountAsync());
        await using (var db = Context(other.Operador.Id))
            Assert.Single(await db.Set<MensajeOutbox>().ToListAsync());
        await using (var db = Scoped(TenantContext.ParaSeguimiento(a.Operador.Id, a.Envio.CuentaComercialId, a.Envio.Id)))
            Assert.Empty(await db.Set<MensajeOutbox>().ToListAsync());
        await using var unscoped = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_testConnection).Options);
        Assert.Empty(await unscoped.Set<MensajeOutbox>().ToListAsync());
    }
}
