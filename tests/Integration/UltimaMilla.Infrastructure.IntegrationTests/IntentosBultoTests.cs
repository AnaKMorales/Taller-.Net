using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private async Task<EscenarioPersistencia> PrepararVisitaParcial(bool reintentable = false,
        AccionBultosPendientes accion = AccionBultosPendientes.NuevaVisitaSinRecargo, decimal? recargo = null)
    {
        var e = await GuardarEscenario(recibido: true, parcial: true, reintentable: reintentable, accion: accion, recargo: recargo);
        await using var db = Context();
        var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
        var ruta = await NuevaRuta(db);
        ruta.AgregarParada(envio, 1, EscenarioPersistencia.Fecha.AddHours(1), envio.Eventos.First().Responsable, EscenarioPersistencia.Fecha);
        db.Add(ruta);
        await db.SaveChangesAsync();
        ruta.Despachar(envio.Eventos.First().Responsable, EscenarioPersistencia.Fecha);
        await db.SaveChangesAsync();
        return e;
    }

    private static ResultadoBultoEntrega[] SolicitudParcial(Envio envio)
    {
        var bultos = envio.Bultos.OrderBy(b => b.Codigo).ToArray();
        PruebaEntrega Foto(string archivo) => new(TipoPrueba.Foto, EscenarioPersistencia.Fecha.AddHours(1),
            new(-34.9m, -56.16m), new ArchivoRef(archivo));
        var foto = Foto("evidencias/entrega.jpg");
        return [new(bultos[0].Id, ResultadoIntento.Entregado, null, [foto]),
            new(bultos[1].Id, ResultadoIntento.Entregado, null, [foto]),
            new(bultos[2].Id, ResultadoIntento.Fallido, envio.Reglas.MotivosNoEntrega.Single(), [Foto("evidencias/fallo.jpg")])];
    }

    [PostgresFact]
    public async Task EntregaMixtaPersisteDetalleEvidenciasYOutboxV2YReenvioNoDuplica()
    {
        await PrepararVisitaParcial();
        var operacion = Guid.NewGuid();
        await using (var db = Context())
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            envio.RegistrarIntentoPorBulto(envio.Parada?.Id ?? envio.Intentos.Single().ParadaId, SolicitudParcial(envio), await db.Set<Repartidor>().SingleAsync(),
                EscenarioPersistencia.Fecha.AddHours(1), operacion);
            await db.SaveChangesAsync();
        }
        await using var reader = Context();
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, actual.Estado);
        Assert.Equal(2, actual.Bultos.Count(b => b.Estado == EstadoBulto.Entregado));
        var intento = Assert.Single(actual.Intentos);
        Assert.Equal(3, intento.Detalles.Count);
        Assert.Equal(2, intento.Pruebas.Count);
        Assert.Equal(3, await reader.Set<PruebaBulto>().CountAsync());
        var evidenciasEntregados = intento.Detalles.Where(d => d.Resultado == ResultadoIntento.Entregado)
            .SelectMany(d => d.Evidencias).ToArray();
        Assert.Same(evidenciasEntregados[0].Prueba, evidenciasEntregados[1].Prueba);
        Assert.NotNull(intento.Detalles.Single(d => d.Resultado == ResultadoIntento.Fallido).Motivo);
        var mensaje = await reader.Set<MensajeOutbox>().SingleAsync(m => m.Tipo == "envio.evento.v2");
        var payload = JsonSerializer.Deserialize<EventoEnvioV2>(mensaje.Contenido, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(2, payload.Version);
        Assert.Equal(3, payload.Bultos.Count);
        Assert.Equal(operacion, payload.IdOperacion);
        Assert.Equal("EntregaParcial", payload.TipoEvento);
        Assert.DoesNotContain(actual.Destinatario.Email, mensaje.Contenido);
        Assert.DoesNotContain(actual.CodigoSeguimiento, mensaje.Contenido);
        Assert.Equal(4, await reader.Set<MensajeOutbox>().CountAsync(m => m.Tipo == "envio.evento.v1"));
        Assert.Same(intento, actual.RegistrarIntentoPorBulto(actual.Parada?.Id ?? actual.Intentos.Single().ParadaId, SolicitudParcial(actual).Reverse(),
            await reader.Set<Repartidor>().SingleAsync(), EscenarioPersistencia.Fecha.AddHours(1), operacion));
        Assert.Equal(0, await reader.SaveChangesAsync());
        Assert.Equal(5, await reader.Set<MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task IntentosCompetidoresNoMezclanEstadosDetallesNiMensajes()
    {
        await PrepararVisitaParcial();
        await using var primero = Context();
        await using var segundo = Context();
        var a = await EscenarioPersistencia.Grafo(primero).SingleAsync();
        var b = await EscenarioPersistencia.Grafo(segundo).SingleAsync();
        a.RegistrarIntentoPorBulto(a.Parada?.Id ?? a.Intentos.Single().ParadaId, SolicitudParcial(a), await primero.Set<Repartidor>().SingleAsync(),
            EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid());
        b.RegistrarIntento(ResultadoIntento.Entregado, null, [new(TipoPrueba.Foto, EscenarioPersistencia.Fecha.AddHours(1),
            new(-34.9m, -56.16m), new ArchivoRef("otra.jpg"))], await segundo.Set<Repartidor>().SingleAsync(),
            EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid());
        await primero.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => segundo.SaveChangesAsync());
        await using var reader = Context();
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, actual.Estado);
        Assert.Single(actual.Intentos);
        Assert.Equal(3, await reader.Set<DetalleIntentoBulto>().CountAsync());
        Assert.Equal(5, await reader.Set<MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task FalloDeOutboxRevierteResultadosPruebasEstadosYVersion()
    {
        await PrepararVisitaParcial();
        await using var db = Context();
        var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
        var version = db.Entry(envio).Property<Guid>("RowVersion").CurrentValue;
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" ADD CONSTRAINT \"FallaV2\" CHECK (\"Tipo\" <> 'envio.evento.v2') NOT VALID");
        envio.RegistrarIntentoPorBulto(envio.Parada?.Id ?? envio.Intentos.Single().ParadaId, SolicitudParcial(envio), await db.Set<Repartidor>().SingleAsync(),
            EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid());
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await using var reader = Context();
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(EstadoEnvio.EnTransito, actual.Estado);
        Assert.All(actual.Bultos, b => Assert.Equal(EstadoBulto.EnTransito, b.Estado));
        Assert.Equal(version, reader.Entry(actual).Property<Guid>("RowVersion").CurrentValue);
        Assert.Empty(await reader.Set<DetalleIntentoBulto>().ToListAsync());
        Assert.Empty(await reader.Set<PruebaBulto>().ToListAsync());
        Assert.Empty(await reader.Set<PruebaEntrega>().ToListAsync());
        Assert.Equal(4, await reader.Set<MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task ReferenciasDeDetalleYPruebasNoSeCruzanNiDentroDelOperador()
    {
        var e = await PrepararVisitaParcial();
        await using (var writer = Context(e.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(writer).SingleAsync();
            envio.RegistrarIntentoPorBulto(envio.Parada?.Id ?? envio.Intentos.Single().ParadaId, SolicitudParcial(envio), await writer.Set<Repartidor>().SingleAsync(),
                EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid());
            await writer.SaveChangesAsync();
            // Otro envío de la misma cuenta y operador: el filtro de tenant por sí solo no basta.
            var otro = new Envio(envio.CuentaComercial, new("Otro", "099000000", "otro@example.com", new("Calle", "1", "Montevideo", "11000")),
                [new("OTRO", 1, 10, 10, 10)], envio.ModalidadServicio, envio.Zona, envio.Reglas, envio.CuadroTarifario,
                150, EscenarioPersistencia.Fecha, envio.FechaComprometida, envio.Programacion, await writer.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id));
            writer.Add(otro); await writer.SaveChangesAsync();
            var detalle = await writer.Set<DetalleIntentoBulto>().FirstAsync();
            var error = await Assert.ThrowsAsync<PostgresException>(() => writer.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"DetallesIntentoBulto\" SET \"BultoId\" = {otro.Bultos.Single().Id} WHERE \"Id\" = {detalle.Id}"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        }
        var ajeno = await GuardarEscenario();
        await CompletarFlujo(ajeno);
        await using var db = Context(e.Operador.Id);
        await using var dbAjeno = Context(ajeno.Operador.Id);
        var pruebaAjena = await dbAjeno.Set<PruebaEntrega>().SingleAsync();
        var vinculo = await db.Set<PruebaBulto>().FirstAsync();
        var fk = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"PruebasBulto\" SET \"PruebaId\" = {pruebaAjena.Id} WHERE \"Id\" = {vinculo.Id}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, fk.SqlState);
    }

    [PostgresFact]
    public async Task DetallesYEvidenciasRespetanCuentaYNoAdmitenReescrituraHistorica()
    {
        var (a, b, ua, _) = await DosCuentas();
        await CompletarFlujo(a); await CompletarFlujo(a, b.Id);
        await using var scope = Scoped(TenantContext.ParaUsuario(a.Operador.Id, ua.Id, RolUsuario.Comercio, a.Envio.CuentaComercialId));
        var detalles = await scope.Set<DetalleIntentoBulto>().Include(d => d.Intento).ThenInclude(i => i.Envio).ToListAsync();
        Assert.NotEmpty(detalles);
        Assert.All(detalles, d => Assert.Equal(a.Envio.Id, d.Intento.EnvioId));
        Assert.NotEmpty(await scope.Set<PruebaBulto>().ToListAsync());
        await using var writer = Context(a.Operador.Id);
        var detalle = await writer.Set<DetalleIntentoBulto>().FirstAsync();
        writer.Entry(detalle).Property(d => d.Resultado).CurrentValue = ResultadoIntento.Entregado;
        await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task MigracionDeResultadosNoInventaAsociacionesHistoricas()
    {
        var e = await GuardarEscenario();
        await CompletarFlujo(e);
        await using (var migration = Context())
        {
            var migrator = migration.GetService<IMigrator>();
            await migrator.MigrateAsync("20261101195520_EstadosPorBulto");
            await migrator.MigrateAsync();
        }
        await using var db = Context();
        var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
        var intento = Assert.Single(envio.Intentos);
        Assert.Single(intento.Pruebas);
        Assert.Empty(intento.Detalles);
        Assert.Null(intento.HuellaSolicitud);
        Assert.Empty(await db.Set<PruebaBulto>().ToListAsync());
        Assert.Equal(EstadoEnvio.Devuelto, envio.Estado);
    }
}
