using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.RegistrarIntento;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private async Task<(EscenarioPersistencia E, RegistrarIntentoCommand Command)> RegistrarPrimeraParcial()
    {
        var e = await PrepararVisitaParcial(reintentable: true);
        var command = await ComandoRegistro(e);
        await RegistroHandler(await IdentidadRepartidor(e)).Handle(command);
        return (e, command);
    }

    private async Task ReprogramarPendientes(EscenarioPersistencia e)
    {
        await using var db = Context(e.Operador.Id);
        var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
        var admin = await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id);
        envio.Reprogramar(ProgramacionEntrega.Crear(envio.Zona.Franjas.Single(), new(2026, 10, 15), TimeSpan.FromHours(-3)),
            admin, EscenarioPersistencia.Fecha.AddHours(2));
        await db.SaveChangesAsync();
    }

    [PostgresFact]
    public async Task PendienteViajaEnVehiculoMenorYSeCompletaDesdeElCasoDeUso()
    {
        var (e, primero) = await RegistrarPrimeraParcial();
        await ReprogramarPendientes(e);
        Guid rutaId;
        Guid segundoRepartidorId;
        await using (var db = Context(e.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            var vehiculo = new Vehiculo(await db.Set<Operador>().SingleAsync(), "PEQUENO", 1, 0.001m);
            var segundoRepartidor = new Repartidor(await db.Set<Operador>().SingleAsync(), "rep-pendientes@example.com");
            segundoRepartidorId = segundoRepartidor.Id;
            var ruta = new Ruta(segundoRepartidor, vehiculo, 5);
            ruta.AgregarParada(envio, 1, EscenarioPersistencia.Fecha.AddDays(1).AddHours(1),
                await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id), EscenarioPersistencia.Fecha.AddHours(2));
            db.Add(ruta); await db.SaveChangesAsync();
            Assert.Equal(1, envio.Parada!.PesoTotalKg);
            Assert.True(envio.PesoTotalKg > vehiculo.CapacidadPesoKg);
            rutaId = ruta.Id;
        }
        await using (var db = Context(e.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            var ruta = await CargarRuta(db, rutaId);
            var admin = await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id);
            EscaneoBulto.RegistrarCarga(ruta, envio.Parada!.Bultos.Single().Bulto, admin, EscenarioPersistencia.Fecha.AddDays(1));
            ruta.Despachar(admin, EscenarioPersistencia.Fecha.AddDays(1));
            await db.SaveChangesAsync();
        }
        RegistrarIntentoCommand segundo;
        await using (var db = Context(e.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            var referencia = Guid.NewGuid();
            segundo = new(envio.Id, envio.Parada!.Id, db.Entry(envio).Property<Guid>("RowVersion").CurrentValue,
                Guid.NewGuid(), EscenarioPersistencia.Fecha.AddDays(1).AddHours(1),
                [new(envio.Parada.Bultos.Single().Bulto.Id, ResultadoIntento.Entregado, null, [referencia])],
                [new(referencia, TipoPrueba.Foto, EscenarioPersistencia.Fecha.AddDays(1).AddHours(1), new(-34.9m, -56.16m), "final.jpg")]);
        }
        var handlerAnterior = RegistroHandler(await IdentidadRepartidor(e));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => handlerAnterior.Handle(segundo));
        var handler = RegistroHandler(await IdentidadRepartidor(e, segundoRepartidorId));
        var resultado = await handler.Handle(segundo);
        Assert.Equal(EstadoEnvio.Entregado, resultado.EstadoActualEnvio);
        Assert.Single(resultado.Bultos);
        Assert.True((await handler.Handle(segundo)).Repetido);
        var repeticionPrimera = await handlerAnterior.Handle(primero);
        Assert.Equal(ResultadoIntento.Parcial, repeticionPrimera.Resultado);
        Assert.Equal(EstadoEnvio.Entregado, repeticionPrimera.EstadoActualEnvio);

        await using var reader = Context(e.Operador.Id);
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(new[] { 3, 1 }, actual.Intentos.OrderBy(i => i.Numero).Select(i => i.Detalles.Count));
        Assert.All(actual.Bultos, b => Assert.Equal(EstadoBulto.Entregado, b.Estado));
        var paradas = await reader.Set<Parada>().Include(p => p.Bultos).ThenInclude(b => b.Bulto).OrderBy(p => p.HoraEstimadaLlegada).ToListAsync();
        Assert.Equal(new[] { 3, 1 }, paradas.Select(p => p.Bultos.Count));
        Assert.Equal(4.1234567m, paradas[0].PesoTotalKg);
        Assert.Equal(0.001m, paradas[1].VolumenTotalM3);
        Assert.Equal(4, await reader.Set<BultoParada>().CountAsync());
        Assert.Equal(9, await reader.Set<MensajeOutbox>().CountAsync());
        var administrativos = await reader.Set<MensajeOutbox>().Include(m => m.Evento)
            .Where(m => m.Evento.IdOperacion == null && m.Evento.EstadoNuevo == EstadoEnvio.EntregadoParcialmente).ToListAsync();
        Assert.Equal(3, administrativos.Count);
        Assert.All(administrativos, m =>
        {
            Assert.Equal("envio.evento.v2", m.Tipo);
            var payload = JsonSerializer.Deserialize<EventoEnvioV2>(m.Contenido, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Null(payload.IdOperacion);
            Assert.Empty(payload.Bultos);
        });
    }

    [PostgresFact]
    public async Task MigracionConservaComposicionAnteriorInclusoTrasEntregaParcial()
    {
        var (e, _) = await RegistrarPrimeraParcial();
        await using (var db = Context(e.Operador.Id))
        {
            // El esquema anterior no registraba medidas por visita: simula una medida actual corregida.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Bultos\" SET \"Peso\" = \"Peso\" + 1 WHERE \"Id\" = {e.Envio.Bultos.First().Id}");
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261101201848_ResultadosEntregaPorBulto");
            await migrator.MigrateAsync();
        }
        await using var reader = Context(e.Operador.Id);
        var parada = await reader.Set<Parada>().Include(p => p.Bultos).ThenInclude(b => b.Bulto).SingleAsync();
        Assert.False(parada.Activa);
        Assert.Equal(3, parada.Bultos.Count);
        Assert.Equal(5.1234567m, parada.PesoTotalKg);
        Assert.Equal(2, parada.Bultos.Count(b => b.Bulto.Estado == EstadoBulto.Entregado));
        Assert.Equal(5, await reader.Set<MensajeOutbox>().CountAsync());
        Assert.Equal(5, await reader.Set<EventoDeEnvio>().CountAsync());
    }

    [PostgresFact]
    public async Task ComposicionRespetaCuentaEsInmutableYNoCruzaEnvios()
    {
        var (a, b, ua, _) = await DosCuentas();
        await CompletarFlujo(a); await CompletarFlujo(a, b.Id);
        await using var cuenta = Scoped(TenantContext.ParaUsuario(a.Operador.Id, ua.Id, RolUsuario.Comercio, a.Envio.CuentaComercialId));
        var propios = await cuenta.Set<BultoParada>().Include(x => x.Parada).ThenInclude(p => p.Envio).ToListAsync();
        Assert.NotEmpty(propios);
        Assert.All(propios, x => Assert.Equal(a.Envio.Id, x.Parada.EnvioId));
        await using var db = Context(a.Operador.Id);
        var miembro = await db.Set<BultoParada>().Where(x => EF.Property<Guid>(x, "EnvioKey") == a.Envio.Id).FirstAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"BultosParada\" SET \"BultoId\" = {b.Bultos.Single().Id} WHERE \"Id\" = {miembro.Id}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        db.Entry(miembro).Property(x => x.PesoKg).CurrentValue += 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        await using var otro = Context(Guid.NewGuid());
        Assert.Empty(await otro.Set<BultoParada>().ToListAsync());
    }

    [PostgresFact]
    public async Task DosRutasCompitenPorElPendienteYSoloUnaConservaAsignacionYComposicion()
    {
        var (e, _) = await RegistrarPrimeraParcial();
        await ReprogramarPendientes(e);
        await using var primera = Context(e.Operador.Id);
        await using var segunda = Context(e.Operador.Id);
        var a = await EscenarioPersistencia.Grafo(primera).SingleAsync();
        var b = await EscenarioPersistencia.Grafo(segunda).SingleAsync();
        var ra = await NuevaRuta(primera);
        var rb = await NuevaRuta(segunda);
        ra.AgregarParada(a, 1, EscenarioPersistencia.Fecha.AddDays(1).AddHours(1), a.Eventos.First().Responsable!, EscenarioPersistencia.Fecha.AddHours(2));
        rb.AgregarParada(b, 1, EscenarioPersistencia.Fecha.AddDays(1).AddHours(1), b.Eventos.First().Responsable!, EscenarioPersistencia.Fecha.AddHours(2));
        primera.Add(ra); segunda.Add(rb);
        var resultados = await Task.WhenAll(Guardar(primera), Guardar(segunda));
        Assert.Single(resultados, r => r);
        Assert.Single(resultados, r => !r);
        await using var reader = Context(e.Operador.Id);
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Single(actual.Parada!.Bultos);
        Assert.Equal(1, actual.Parada.PesoTotalKg);
        Assert.Equal(4, await reader.Set<BultoParada>().CountAsync());
        Assert.Equal(2, await reader.Set<Parada>().CountAsync());
        Assert.Equal(7, await reader.Set<MensajeOutbox>().CountAsync());

        static async Task<bool> Guardar(AppDbContext db)
        {
            try { await db.SaveChangesAsync(); return true; }
            catch (ConflictoConcurrenciaException) { return false; }
        }
    }
}
