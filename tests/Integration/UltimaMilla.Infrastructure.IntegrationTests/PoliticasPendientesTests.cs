using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private async Task<(EscenarioPersistencia E, Guid RutaId)> PrepararVisitaConRecargo()
    {
        var e = await PrepararVisitaParcial(true, AccionBultosPendientes.NuevaVisitaConRecargo, 25.50m);
        await RegistroHandler(await IdentidadRepartidor(e)).Handle(await ComandoRegistro(e));
        await ReprogramarPendientes(e);
        await using var db = Context(e.Operador.Id);
        var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
        var ruta = await NuevaRuta(db);
        ruta.AgregarParada(envio, 1, EscenarioPersistencia.Fecha.AddDays(1).AddHours(1),
            await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id), EscenarioPersistencia.Fecha.AddHours(2));
        db.Add(ruta); await db.SaveChangesAsync();
        Assert.Empty(envio.Recargos);
        return (e, ruta.Id);
    }

    private static async Task DespacharVisita(AppDbContext db, Guid rutaId, Guid adminId)
    {
        await EscenarioPersistencia.Grafo(db).SingleAsync();
        var ruta = await CargarRuta(db, rutaId);
        ruta.Despachar(await db.Set<Usuario>().SingleAsync(u => u.Id == adminId), EscenarioPersistencia.Fecha.AddDays(1));
    }

    [PostgresFact]
    public async Task RecargoFijoSePersisteUnaVezEsHistoricoYRespetaCuentaYOperador()
    {
        var (e, rutaId) = await PrepararVisitaConRecargo();
        await using (var db = Context(e.Operador.Id))
        {
            await DespacharVisita(db, rutaId, e.Admin.Id);
            await db.SaveChangesAsync();
        }
        await using var reader = Context(e.Operador.Id);
        var envio = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        var recargo = Assert.Single(envio.Recargos);
        Assert.Equal(25.50m, recargo.Importe);
        Assert.Equal(25.50m, envio.Parada!.RecargoPrevisto);
        Assert.Same(recargo, envio.Parada.Recargo);
        Assert.Equal(150.25m, envio.TarifaCalculada);
        Assert.Equal(8, await reader.Set<MensajeOutbox>().CountAsync());
        Assert.Equal(0, await reader.SaveChangesAsync());
        var usuarioComercio = e.Envio.CuentaComercial.Usuarios.Single().Id;
        await using var propia = Scoped(TenantContext.ParaUsuario(e.Operador.Id, usuarioComercio, RolUsuario.Comercio, envio.CuentaComercialId));
        Assert.Single(await propia.Set<RecargoVisita>().ToListAsync());
        await using var ajena = Scoped(TenantContext.ParaUsuario(e.Operador.Id, usuarioComercio, RolUsuario.Comercio, Guid.NewGuid()));
        Assert.Empty(await ajena.Set<RecargoVisita>().ToListAsync());
        await using var otro = Context(Guid.NewGuid());
        Assert.Empty(await otro.Set<RecargoVisita>().ToListAsync());
        var duplicado = await Assert.ThrowsAsync<PostgresException>(() => reader.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "RecargosVisita" ("Id", "EnvioId", "TenantId", "ParadaId", "Importe", "RegistradoEn")
            VALUES ({Guid.NewGuid()}, {envio.Id}, {e.Operador.Id}, {envio.Parada.Id}, {25.50m}, {recargo.RegistradoEn})
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicado.SqlState);
        reader.Entry(recargo).Property(x => x.Importe).CurrentValue = 90;
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task FallaDelOutboxRevierteRecargoDespachoEstadosYVersion()
    {
        var (e, rutaId) = await PrepararVisitaConRecargo();
        Guid version;
        await using (var db = Context(e.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            version = db.Entry(envio).Property<Guid>("RowVersion").CurrentValue;
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" ADD CONSTRAINT \"FalloRecargo\" CHECK (\"Tipo\" <> 'envio.evento.v2') NOT VALID");
            await DespacharVisita(db, rutaId, e.Admin.Id);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var reader = Context(e.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(reader).SingleAsync();
            Assert.Empty(envio.Recargos);
            Assert.Equal(version, reader.Entry(envio).Property<Guid>("RowVersion").CurrentValue);
            Assert.Equal(EstadoBulto.AsignadoARuta, envio.Parada!.Bultos.Single().Bulto.Estado);
            Assert.Null((await CargarRuta(reader, rutaId)).DespachadaEn);
            Assert.Equal(7, await reader.Set<MensajeOutbox>().CountAsync());
            await reader.Database.ExecuteSqlRawAsync("ALTER TABLE \"MensajesOutbox\" DROP CONSTRAINT \"FalloRecargo\"");
        }
        await using var retry = Context(e.Operador.Id);
        await DespacharVisita(retry, rutaId, e.Admin.Id);
        await retry.SaveChangesAsync();
        Assert.Single(await retry.Set<RecargoVisita>().ToListAsync());
    }

    [PostgresFact]
    public async Task DespachosConcurrentesNoDuplicanRecargo()
    {
        var (e, rutaId) = await PrepararVisitaConRecargo();
        await using var a = Context(e.Operador.Id);
        await using var b = Context(e.Operador.Id);
        await DespacharVisita(a, rutaId, e.Admin.Id);
        await DespacharVisita(b, rutaId, e.Admin.Id);
        await a.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => b.SaveChangesAsync());
        await using var reader = Context(e.Operador.Id);
        Assert.Single(await reader.Set<RecargoVisita>().ToListAsync());
        Assert.Equal(8, await reader.Set<MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task DevolucionParcialSeReleeCompletaYNoDevuelveBultosEntregados()
    {
        var e = await PrepararVisitaParcial(true, AccionBultosPendientes.DevolverAlComercio);
        var command = await ComandoRegistro(e);
        var fallo = command.Bultos.Single(b => b.Resultado == ResultadoIntento.Fallido);
        var segundoId = e.Envio.Bultos.Single(b => b.Codigo == "B002").Id;
        await RegistroHandler(await IdentidadRepartidor(e)).Handle(command with
        {
            Bultos = command.Bultos.Select(b => b.BultoId == segundoId
                ? b with { Resultado = ResultadoIntento.Fallido, MotivoId = fallo.MotivoId, Evidencias = fallo.Evidencias } : b).ToArray()
        });
        await using (var db = Context(e.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            envio.IniciarDevolucion(await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id), EscenarioPersistencia.Fecha.AddHours(2));
            await db.SaveChangesAsync();
        }
        // Una carga filtrada no debe permitir cerrar dos pendientes escaneando solo uno.
        await using (var incompleta = Context(e.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(incompleta, incluirDevolucion: false)
                .Include(x => x.Devolucion).ThenInclude(d => d!.Bultos.Where(b => b.Bulto.Id == segundoId)).ThenInclude(b => b.Bulto).SingleAsync();
            var admin = await incompleta.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id);
            var miembro = Assert.Single(envio.Devolucion!.Bultos);
            EscaneoBulto.RegistrarDevolucion(envio.Devolucion, miembro.Bulto, admin, EscenarioPersistencia.Fecha.AddHours(3));
            Assert.Throws<InvalidOperationException>(() => envio.Devolucion.Completar(admin, EscenarioPersistencia.Fecha.AddHours(3)));
        }
        await using (var db = Context(e.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            var admin = await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id);
            var devolucion = envio.Devolucion!;
            Assert.Equal(2, devolucion.Bultos.Count);
            Assert.All(devolucion.Bultos, b => Assert.Equal(EstadoBulto.EnDevolucion, b.Bulto.Estado));
            Assert.Throws<InvalidOperationException>(() => devolucion.Completar(admin, EscenarioPersistencia.Fecha.AddHours(3)));
            foreach (var miembro in devolucion.Bultos)
                EscaneoBulto.RegistrarDevolucion(devolucion, miembro.Bulto, admin, EscenarioPersistencia.Fecha.AddHours(3), envio.Intentos.Single().Parada.Ruta);
            devolucion.Completar(admin, EscenarioPersistencia.Fecha.AddHours(3));
            await db.SaveChangesAsync();
        }
        await using var reader = Context(e.Operador.Id);
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, actual.Estado);
        Assert.Equal(EstadoDevolucion.Completado, actual.Devolucion!.Estado);
        Assert.Single(actual.Bultos, b => b.Estado == EstadoBulto.Entregado);
        Assert.Equal(2, actual.Bultos.Count(b => b.Estado == EstadoBulto.Devuelto));
        Assert.Equal(7, await reader.Set<MensajeOutbox>().CountAsync());
        await using var otro = Context(Guid.NewGuid());
        Assert.Empty(await otro.Set<BultoDevolucion>().ToListAsync());
        reader.Remove(actual.Devolucion.Bultos.First());
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task MigracionRecuperaDevolucionesPreviasSinInventarRecargos()
    {
        var e = await GuardarEscenario();
        await CompletarFlujo(e);
        await using (var db = Context(e.Operador.Id))
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261103121131_BultosPorParada");
            await migrator.MigrateAsync();
        }
        await using var reader = Context(e.Operador.Id);
        var envio = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(envio.Bultos.Single().Id, Assert.Single(envio.Devolucion!.Bultos).Bulto.Id);
        Assert.Equal(EstadoDevolucion.Completado, envio.Devolucion.Estado);
        Assert.Empty(envio.Recargos);
        Assert.Null(envio.Reglas.ImporteRecargoNuevaVisita);
        Assert.All(await reader.Set<Parada>().ToListAsync(), p => Assert.Equal(0, p.RecargoPrevisto));
    }

    [PostgresFact]
    public async Task ReglasConRecargoRechazanImportesInvalidosYLaVersionAnteriorSinImporteNoCobra()
    {
        var e = await PrepararVisitaParcial(true, AccionBultosPendientes.NuevaVisitaConRecargo, 25.50m);
        var gestion = Gestion(e);
        foreach (var importe in new decimal?[] { null, 0, -1, 0.001m })
            await Assert.ThrowsAsync<ArgumentException>(() => gestion.CrearReglasAsync(new(1, null,
                DatosReglas() with { PermiteEntregaParcial = true, AccionPendientes = AccionBultosPendientes.NuevaVisitaConRecargo,
                    ImporteRecargoNuevaVisita = importe })));
        Assert.Single(await gestion.ListarReglasAsync());
        await RegistroHandler(await IdentidadRepartidor(e)).Handle(await ComandoRegistro(e));
        await using (var db = Context(e.Operador.Id))
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"ReglasOperativas\" SET \"ImporteRecargoNuevaVisita\" = 1.001 WHERE \"Id\" = {e.Reglas.Id}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261103121131_BultosPorParada");
            await migrator.MigrateAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReprogramarPendientes(e));
        await using var reader = Context(e.Operador.Id);
        Assert.Empty(await reader.Set<RecargoVisita>().ToListAsync());
        Assert.Equal(5, await reader.Set<MensajeOutbox>().CountAsync());
    }
}
