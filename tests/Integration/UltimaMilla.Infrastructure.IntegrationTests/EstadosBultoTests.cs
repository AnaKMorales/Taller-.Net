using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task MigracionInfiereEstadosPreviosPorEnvioYOperadorSinEmitirMensajes()
    {
        var anteriores = new List<(Guid Operador, Guid Envio, int Estado)>();
        // Simula las diez situaciones válidas del sistema anterior, en operadores separados.
        foreach (var estado in Enumerable.Range(0, 10))
        {
            var e = await GuardarEscenario();
            anteriores.Add((e.Operador.Id, e.Envio.Id, estado));
            await using var db = Context(e.Operador.Id);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Envios\" SET \"Estado\" = {estado} WHERE \"Id\" = {e.Envio.Id}");
        }
        await using (var migration = Context())
        {
            var migrator = migration.GetService<IMigrator>();
            await migrator.MigrateAsync("20261101185636_ReglasEntregaParcial");
            await migrator.MigrateAsync();
        }
        foreach (var anterior in anteriores)
        {
            await using var db = Context(anterior.Operador);
            var envio = await db.Envios.Include(x => x.Bultos).SingleAsync();
            Assert.Equal(anterior.Envio, envio.Id);
            Assert.Equal(anterior.Estado, (int)envio.Bultos.Single().Estado);
            Assert.Equal(envio.Estado, EstadoConjuntoEnvio.Calcular(envio.Bultos.Select(b => b.Estado)));
            Assert.Single(await db.Set<EventoDeEnvio>().ToListAsync());
            Assert.Single(await db.Set<MensajeOutbox>().ToListAsync());
        }
    }

    [PostgresFact]
    public async Task EstadosBultoPersistenEnEntregaYConflictoNoDejaEstadosPerdedores()
    {
        var e = await GuardarEscenario(recibido: true);
        await using var ganador = Context();
        await using var perdedor = Context();
        var a = await EscenarioPersistencia.Grafo(ganador).SingleAsync();
        var b = await EscenarioPersistencia.Grafo(perdedor).SingleAsync();
        var ruta = await NuevaRuta(ganador);
        ruta.AgregarParada(a, 1, EscenarioPersistencia.Fecha.AddHours(1), a.Eventos.First().Responsable!, EscenarioPersistencia.Fecha);
        ganador.Add(ruta);
        b.MarcarExtraviado(b.Eventos.First().Responsable!, EscenarioPersistencia.Fecha, "Decisión perdedora");
        await ganador.SaveChangesAsync();
        await Assert.ThrowsAsync<UltimaMilla.Application.Abstractions.ConflictoConcurrenciaException>(() => perdedor.SaveChangesAsync());
        await using var reader = Context();
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(EstadoEnvio.AsignadoARuta, actual.Estado);
        Assert.Equal(EstadoBulto.AsignadoARuta, actual.Bultos.Single().Estado);
        Assert.DoesNotContain(actual.Eventos, x => x.Tipo == TipoEvento.EventoAnomalo);
        Assert.Equal(3, await reader.Set<MensajeOutbox>().CountAsync());
        await using var otro = Context(Guid.NewGuid());
        Assert.Empty(await otro.Set<Bulto>().ToListAsync());
        Assert.Equal(e.Operador.Id, reader.Entry(actual.Bultos.Single()).Property<Guid>("TenantId").CurrentValue);
    }

    [PostgresFact]
    public async Task PostgreSqlRechazaEstadosBultoDesconocidos()
    {
        var e = await GuardarEscenario();
        await using var db = Context();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Bultos\" SET \"Estado\" = 999 WHERE \"Id\" = {e.Envio.Bultos.Single().Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("CK_Bultos_Estado", error.ConstraintName);
    }
}
