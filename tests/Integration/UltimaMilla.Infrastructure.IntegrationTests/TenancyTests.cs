using UltimaMilla.Application.Tenancy;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task DosOperadoresConComercioCompartidoSoloVenSuGrafoCompleto()
    {
        var (a, b) = await DosOperadores();
        await CompletarFlujo(a);
        await CompletarFlujo(b);
        // MensajesProcesados también es dato por operador: se siembra una fila para cada uno.
        await Registro().EjecutarUnaVezAsync(TenantDe(a), Guid.NewGuid(), "avisos", _ => Task.CompletedTask);
        await Registro().EjecutarUnaVezAsync(TenantDe(b), Guid.NewGuid(), "avisos", _ => Task.CompletedTask);
        await using var first = Context(a.Operador.Id);
        await using var second = Context(b.Operador.Id);
        Assert.Same(first.Model, second.Model);
        Assert.Equal(a.Envio.Id, (await EscenarioPersistencia.Grafo(first).SingleAsync()).Id);
        Assert.Equal(b.Envio.Id, (await EscenarioPersistencia.Grafo(second).SingleAsync()).Id);
        Assert.Null(await first.Envios.FindAsync(b.Envio.Id));
        Assert.Null(await first.Set<Bulto>().FindAsync(b.Envio.Bultos.Single().Id));
        Assert.Null(await first.Set<Usuario>().FindAsync(b.Admin.Id));
        Assert.Equal(a.Envio.ComercioId, (await first.Set<Comercio>().SingleAsync()).Id);
        Assert.Equal(a.Envio.ComercioId, (await second.Set<Comercio>().SingleAsync()).Id);
        var comercio = await first.Set<Comercio>().Include(c => c.CuentasComerciales).SingleAsync();
        Assert.Single(comercio.CuentasComerciales);
        Assert.Equal(a.Operador.Id, comercio.CuentasComerciales.Single().OperadorId);
        foreach (var type in first.Model.GetEntityTypes().Where(t => t.BaseType is null && t.FindProperty("TenantId") is not null))
        {
            var method = typeof(PostgresTests).GetMethod(nameof(TenantsVisibles), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(type.ClrType);
            var tenantsA = await (Task<List<Guid>>)method.Invoke(null, [first, type.Name])!;
            var tenantsB = await (Task<List<Guid>>)method.Invoke(null, [second, type.Name])!;
            if (type.ClrType != typeof(RecargoVisita) && type.GetTableName() != "AltasImportadas")
            {
                Assert.NotEmpty(tenantsA);
                Assert.NotEmpty(tenantsB);
            }
            // Este escenario no genera recargos ni importa archivos. La importación tiene sus propias pruebas de aislamiento.
            else { Assert.Empty(tenantsA); Assert.Empty(tenantsB); }
            Assert.All(tenantsA, id => Assert.Equal(a.Operador.Id, id));
            Assert.All(tenantsB, id => Assert.Equal(b.Operador.Id, id));
        }
        Assert.IsType<Repartidor>(await first.Set<Repartidor>().SingleAsync());
    }

    [PostgresFact]
    public async Task SinOperadorNoSeDevuelvenDatosYNoSePermiteGuardar()
    {
        await DosOperadores();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_testConnection).Options;
        await using var db = new AppDbContext(options);
        Assert.Empty(await db.Envios.ToListAsync());
        Assert.Empty(await db.Set<Comercio>().ToListAsync());
        Assert.Empty(await db.Set<Bulto>().ToListAsync());
        Assert.Empty(await db.Set<Operador>().ToListAsync());
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task ElContextoNoCambiaDeOperadorAunqueCambieElServicio()
    {
        var (a, b) = await DosOperadores();
        var identity = new IdentidadMutable { OperadorId = a.Operador.Id };
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_testConnection).Options, identity);
        Assert.Equal(a.Envio.Id, (await db.Envios.SingleAsync()).Id);
        identity.OperadorId = b.Operador.Id;
        Assert.Equal(a.Envio.Id, (await db.Envios.AsNoTracking().SingleAsync()).Id);
    }

    [PostgresFact]
    public async Task RechazaAltasYRelacionesDeOtroOperadorAntesDeEscribir()
    {
        var (a, b) = await DosOperadores();
        await using (var db = Context(a.Operador.Id))
        {
            var ajeno = new EscenarioPersistencia();
            db.Add(ajeno.Envio);
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        await using (var db = Context(a.Operador.Id))
        {
            var envio = await db.Envios.SingleAsync();
            db.Entry(envio).Property("ZonaKey").CurrentValue = b.Zona.Id;
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        await using (var db = Context(a.Operador.Id))
        {
            var bulto = new Bulto("Ajeno", 1, 10, 10, 10);
            db.Add(bulto);
            db.Entry(bulto).Property("EnvioKey").CurrentValue = b.Envio.Id;
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        await using var reader = Context(a.Operador.Id);
        Assert.Equal(a.Zona.Id, await reader.Envios.Select(e => EF.Property<Guid>(e, "ZonaKey")).SingleAsync());
        Assert.Single(await reader.Set<Bulto>().ToListAsync());
    }

    [PostgresFact]
    public async Task RechazaModificarYEliminarAjenoInclusoSiSeFalsificaElTenantDelObjeto()
    {
        var (a, b) = await DosOperadores();
        foreach (var delete in new[] { false, true })
        {
            await using var db = Context(a.Operador.Id);
            var stub = new Bulto("Objeto desconectado", 1, 10, 10, 10);
            db.Entry(stub).Property("Id").CurrentValue = b.Envio.Bultos.Single().Id;
            db.Entry(stub).Property("TenantId").CurrentValue = a.Operador.Id;
            db.Attach(stub);
            if (delete) db.Remove(stub);
            else db.Entry(stub).Property(nameof(Bulto.Peso)).CurrentValue = 999m;
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        await using var reader = Context(b.Operador.Id);
        Assert.Equal(2.1234567m, await reader.Set<Bulto>().Select(p => p.Peso).SingleAsync());
    }

    [PostgresFact]
    public async Task TablaDeUsuariosYCuentasTampocoAdmiteVinculosCruzados()
    {
        var (a, b) = await DosOperadores();
        await using var db = Context(a.Operador.Id);
        db.Set<Dictionary<string, object>>("UsuariosCuentasComerciales").Add(new()
        {
            ["UsuarioId"] = a.Admin.Id,
            ["CuentaComercialId"] = b.Envio.CuentaComercialId
        });
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task PostgreSqlRechazaReferenciasCruzadasInclusoConSqlDirecto()
    {
        var (a, b) = await DosOperadores();
        await using var db = Context(a.Operador.Id);
        var cross = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Envios\" SET \"ZonaId\" = {b.Zona.Id} WHERE \"Id\" = {a.Envio.Id}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, cross.SqlState);
        var owner = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"Usuarios\" SET \"TenantId\" = {b.Operador.Id} WHERE \"Id\" = {a.Admin.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, owner.SqlState);
    }

    [PostgresFact]
    public async Task UnOperadorNoEditaNiBorraLosDatosGlobalesDelComercioCompartido()
    {
        var (a, b) = await DosOperadores();
        await using (var db = Context(a.Operador.Id))
        {
            var comercio = await db.Set<Comercio>().SingleAsync();
            db.Entry(comercio).Property(nameof(Comercio.RazonSocial)).CurrentValue = "Cambio global";
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        await using (var db = Context(a.Operador.Id))
        {
            db.Remove(await db.Set<Comercio>().SingleAsync());
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        await using var reader = Context(b.Operador.Id);
        Assert.Equal("Comercio", await reader.Set<Comercio>().Select(c => c.RazonSocial).SingleAsync());
    }

    [PostgresFact]
    public async Task MigracionRecuperaTenantDeDatosExistentesSinPerderElGrafo()
    {
        var (a, b) = await DosOperadores();
        await CompletarFlujo(a);
        await CompletarFlujo(b);
        await using var db = Context(a.Operador.Id);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261014230137_InitialCreate");
        await migrator.MigrateAsync();
        await using var first = Context(a.Operador.Id);
        await using var second = Context(b.Operador.Id);
        Assert.Equal(a.Envio.Id, (await EscenarioPersistencia.Grafo(first).SingleAsync()).Id);
        Assert.Equal(b.Envio.Id, (await EscenarioPersistencia.Grafo(second).SingleAsync()).Id);
        Assert.Equal(EstadoEnvio.Devuelto, (await first.Envios.SingleAsync()).Estado);
        Assert.Single(await first.Set<PruebaEntrega>().ToListAsync());
        Assert.Single(await second.Set<EntregaAviso>().ToListAsync());
    }

    private async Task<(EscenarioPersistencia A, EscenarioPersistencia B)> DosOperadores()
    {
        var a = new EscenarioPersistencia();
        await using (var db = Context(a.Operador.Id))
        {
            db.Add(a.Envio);
            await db.SaveChangesAsync();
        }
        Comercio shared;
        await using (var db = Context(a.Operador.Id)) shared = await db.Set<Comercio>().AsNoTracking().SingleAsync();
        var b = new EscenarioPersistencia(comercio: shared);
        await using (var db = Context(b.Operador.Id))
        {
            db.Entry(shared).State = EntityState.Unchanged;
            db.Add(b.Envio);
            await db.SaveChangesAsync();
        }
        return (a, b);
    }

    private async Task CompletarFlujo(EscenarioPersistencia scenario, Guid? envioId = null)
    {
        await using var db = Context(scenario.Operador.Id);
        var targetId = envioId ?? scenario.Envio.Id;
        var envio = await EscenarioPersistencia.Grafo(db).SingleAsync(e => e.Id == targetId);
        var admin = await db.Set<Usuario>().SingleAsync(u => u.Id == scenario.Admin.Id);
        var repartidor = await db.Set<Repartidor>().SingleAsync();
        var vehiculo = await db.Set<Vehiculo>().SingleAsync();
        var fecha = EscenarioPersistencia.Fecha;
        EscaneoBulto.RegistrarRecepcion(envio.Bultos.Single(), admin, fecha);
        envio.RecibirEnDeposito(admin, fecha);
        var ruta = new Ruta(repartidor, vehiculo, 10);
        ruta.AgregarParada(envio, 1, fecha.AddHours(1), admin, fecha);
        db.Add(ruta);
        await db.SaveChangesAsync();
        ruta.Despachar(admin, fecha);
        envio.RegistrarIntento(ResultadoIntento.Fallido, envio.Reglas.MotivosNoEntrega.Single(),
            [scenario.Foto()], repartidor, fecha.AddHours(1), Guid.NewGuid());
        await db.SaveChangesAsync();
        var devolucion = envio.IniciarDevolucion(admin, fecha.AddHours(1));
        EscaneoBulto.RegistrarDevolucion(devolucion, envio.Bultos.Single(), admin, fecha.AddHours(2), ruta);
        devolucion.Completar(admin, fecha.AddHours(2));
        ruta.Rendir(fecha.AddHours(2));
        _ = new Discrepancia(envio, envio.Bultos.Single(), TipoDiscrepancia.PesoNoCoincide, "Peso", "2", "3", fecha.AddHours(2));
        var subscription = new SuscripcionAviso(envio.CuentaComercial, [TipoEvento.DevolucionExitosa], "prueba", new Uri("https://example.com/webhook"));
        _ = new EntregaAviso(subscription, envio.Eventos.Last());
        _ = new Notificacion(envio.Destinatario, CanalNotificacion.Email, "Devuelto");
        await db.SaveChangesAsync();
    }

    private static Task<List<Guid>> TenantsVisibles<T>(AppDbContext db, string name) where T : class
    {
        var query = typeof(T) == typeof(Dictionary<string, object>) ? db.Set<T>(name) : db.Set<T>();
        return query.Select(row => EF.Property<Guid>(row, "TenantId")).ToListAsync();
    }

    private sealed class IdentidadMutable : ITenantActual
    {
        public Guid? OperadorId { get; set; }
        public TenantContext? Contexto => OperadorId is Guid id ? TenantContext.ParaProcesoInterno(id) : null;
    }
}
