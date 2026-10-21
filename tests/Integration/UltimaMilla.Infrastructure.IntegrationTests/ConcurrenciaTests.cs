using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task DosDecisionesSobreElMismoEnvioNoSobrescribenEstadoNiHistorial()
    {
        var e = await GuardarEscenario(recibido: true);
        await using var first = Context();
        await using var second = Context();
        var a = await EscenarioPersistencia.Grafo(first).SingleAsync();
        var b = await EscenarioPersistencia.Grafo(second).SingleAsync();
        var ruta = await NuevaRuta(first);
        ruta.AgregarParada(a, 1, EscenarioPersistencia.Fecha.AddHours(1), a.Eventos.First().Responsable, EscenarioPersistencia.Fecha);
        first.Add(ruta);
        b.MarcarExtraviado(b.Eventos.First().Responsable, EscenarioPersistencia.Fecha, "Decisión desactualizada");
        await first.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => second.SaveChangesAsync());
        Assert.IsType<DbUpdateConcurrencyException>(error.InnerException);
        await using var reader = Context();
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(EstadoEnvio.AsignadoARuta, actual.Estado);
        Assert.Equal(3, actual.Eventos.Count);
        Assert.DoesNotContain(actual.Eventos, v => v.Tipo == TipoEvento.EventoAnomalo);
        Assert.Equal(e.Envio.Id, actual.Id);
    }

    [PostgresFact]
    public async Task DosDespachadoresAsignanElMismoEnvioYSoloUnoConfirma()
    {
        await GuardarEscenario(recibido: true);
        await using var first = Context();
        await using var second = Context();
        var a = await EscenarioPersistencia.Grafo(first).SingleAsync();
        var b = await EscenarioPersistencia.Grafo(second).SingleAsync();
        var ra = await NuevaRuta(first);
        var rb = await NuevaRuta(second);
        ra.AgregarParada(a, 1, EscenarioPersistencia.Fecha.AddHours(1), a.Eventos.First().Responsable, EscenarioPersistencia.Fecha);
        rb.AgregarParada(b, 1, EscenarioPersistencia.Fecha.AddHours(1), b.Eventos.First().Responsable, EscenarioPersistencia.Fecha);
        first.Add(ra); second.Add(rb);
        var results = await Task.WhenAll(Confirmar(first), Confirmar(second));
        Assert.Single(results, ok => ok);
        Assert.Single(results, ok => !ok);
        await using var reader = Context();
        var actual = await reader.Envios.SingleAsync();
        Assert.Equal(EstadoEnvio.AsignadoARuta, actual.Estado);
        Assert.Single(await reader.Set<Ruta>().ToListAsync());
        Assert.Single(await reader.Set<Parada>().Where(p => p.Activa).ToListAsync());
        Assert.Equal(3, await reader.Set<EventoDeEnvio>().CountAsync());
        static async Task<bool> Confirmar(AppDbContext db)
        {
            try { await db.SaveChangesAsync(); return true; }
            catch (ConflictoConcurrenciaException) { return false; }
        }
    }

    [PostgresFact]
    public async Task DosEnviosDistintosNoSuperanCapacidadDeLaMismaRuta()
    {
        var (a, b, _, _) = await DosCuentas();
        _operatorId = a.Operador.Id;
        Guid routeId;
        await using (var setup = Context())
        {
            foreach (var envio in await EscenarioPersistencia.Grafo(setup).ToListAsync())
            {
                var admin = envio.Eventos.Single().Responsable;
                EscaneoBulto.RegistrarRecepcion(envio.Bultos.Single(), admin, EscenarioPersistencia.Fecha);
                envio.RecibirEnDeposito(admin, EscenarioPersistencia.Fecha);
            }
            var ruta = await NuevaRuta(setup);
            setup.Entry(ruta.Vehiculo).Property(nameof(Vehiculo.CapacidadPesoKg)).CurrentValue = 4m;
            setup.Add(ruta);
            await setup.SaveChangesAsync();
            routeId = ruta.Id;
        }
        await using var first = Context();
        await using var second = Context();
        var ea = await EscenarioPersistencia.Grafo(first).SingleAsync(e => e.Id == a.Envio.Id);
        var eb = await EscenarioPersistencia.Grafo(second).SingleAsync(e => e.Id == b.Id);
        var ra = await CargarRuta(first, routeId);
        var rb = await CargarRuta(second, routeId);
        Assert.Empty(ra.Paradas); Assert.Empty(rb.Paradas);
        ra.AgregarParada(ea, 1, EscenarioPersistencia.Fecha.AddHours(1), ea.Eventos.First().Responsable, EscenarioPersistencia.Fecha);
        rb.AgregarParada(eb, 2, EscenarioPersistencia.Fecha.AddHours(1), eb.Eventos.First().Responsable, EscenarioPersistencia.Fecha);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => second.SaveChangesAsync());
        await using var reader = Context();
        var actual = await CargarRuta(reader, routeId);
        Assert.Single(actual.Paradas);
        Assert.True(actual.Paradas.Sum(p => p.Envio.PesoTotalKg) <= actual.Vehiculo.CapacidadPesoKg);
        Assert.Equal(EstadoEnvio.EnDeposito, (await reader.Envios.SingleAsync(e => e.Id == b.Id)).Estado);
        var pendiente = await EscenarioPersistencia.Grafo(reader).SingleAsync(e => e.Id == b.Id);
        var rejected = Assert.Throws<InvalidOperationException>(() => actual.AgregarParada(pendiente, 2,
            EscenarioPersistencia.Fecha.AddHours(1), pendiente.Eventos.First().Responsable, EscenarioPersistencia.Fecha));
        Assert.Contains("capacidad", rejected.Message);
    }

    [PostgresFact]
    public async Task CambiarSoloUnHijoTambienCompitePorLaVersionDelEnvio()
    {
        await GuardarEscenario();
        await using var first = Context();
        await using var second = Context();
        var a = await EscenarioPersistencia.Grafo(first).SingleAsync();
        var b = await EscenarioPersistencia.Grafo(second).SingleAsync();
        EscaneoBulto.RegistrarRecepcion(a.Bultos.Single(), a.Eventos.Single().Responsable, EscenarioPersistencia.Fecha);
        EscaneoBulto.RegistrarRecepcion(b.Bultos.Single(), b.Eventos.Single().Responsable, EscenarioPersistencia.Fecha);
        b.RecibirEnDeposito(b.Eventos.Single().Responsable, EscenarioPersistencia.Fecha);
        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => second.SaveChangesAsync());
        await using var reader = Context();
        Assert.Equal(EstadoEnvio.Admitido, (await reader.Envios.SingleAsync()).Estado);
        Assert.Single(await reader.Set<EscaneoBulto>().ToListAsync());
        Assert.Single(await reader.Set<EventoDeEnvio>().ToListAsync());
    }

    [PostgresFact]
    public async Task GuardadoSincronoDetectaVersionViejaYContextoNuevoPuedeContinuar()
    {
        await GuardarEscenario(recibido: true);
        using var first = Context();
        using var second = Context();
        var a = await EscenarioPersistencia.Grafo(first).SingleAsync();
        var b = await EscenarioPersistencia.Grafo(second).SingleAsync();
        first.Entry(a.Bultos.Single()).Property(nameof(Bulto.Peso)).CurrentValue = 3m;
        second.Entry(b.Bultos.Single()).Property(nameof(Bulto.Peso)).CurrentValue = 4m;
        first.SaveChanges();
        Assert.Throws<ConflictoConcurrenciaException>(() => second.SaveChanges());
        await using var fresh = Context();
        var actual = await EscenarioPersistencia.Grafo(fresh).SingleAsync();
        Assert.Equal(3m, actual.Bultos.Single().Peso);
        actual.MarcarExtraviado(actual.Eventos.First().Responsable, EscenarioPersistencia.Fecha, "Decisión revisada");
        await fresh.SaveChangesAsync();
        await using var reader = Context();
        Assert.Equal(EstadoEnvio.Extraviado, (await reader.Envios.SingleAsync()).Estado);
    }

    [PostgresFact]
    public async Task GuardarSinCambiosNoInvalidaOtraOperacion()
    {
        await GuardarEscenario(recibido: true);
        await using var first = Context();
        await using var second = Context();
        var a = await EscenarioPersistencia.Grafo(first).SingleAsync();
        var b = await EscenarioPersistencia.Grafo(second).SingleAsync();
        var original = first.Entry(a).Property<Guid>("RowVersion").CurrentValue;
        Assert.NotEqual(Guid.Empty, original);
        Assert.Equal(0, await first.SaveChangesAsync());
        Assert.Equal(original, first.Entry(a).Property<Guid>("RowVersion").CurrentValue);
        b.MarcarExtraviado(b.Eventos.First().Responsable, EscenarioPersistencia.Fecha, "Válido");
        await second.SaveChangesAsync();
        var next = second.Entry(b).Property<Guid>("RowVersion").CurrentValue;
        Assert.NotEqual(original, next);
        Assert.Equal(next, second.Entry(b).Property<Guid>("RowVersion").OriginalValue);
    }

    [PostgresFact]
    public async Task EnviosIndependientesNoCompartenVersion()
    {
        var (a, b, _, _) = await DosCuentas();
        _operatorId = a.Operador.Id;
        await using var first = Context();
        await using var second = Context();
        var ea = await EscenarioPersistencia.Grafo(first).SingleAsync(e => e.Id == a.Envio.Id);
        var eb = await EscenarioPersistencia.Grafo(second).SingleAsync(e => e.Id == b.Id);
        first.Entry(ea.Bultos.Single()).Property(nameof(Bulto.Peso)).CurrentValue = 3m;
        second.Entry(eb.Bultos.Single()).Property(nameof(Bulto.Peso)).CurrentValue = 4m;
        await Task.WhenAll(first.SaveChangesAsync(), second.SaveChangesAsync());
        await using var reader = Context();
        Assert.Equal(new[] { 3m, 4m }, await reader.Set<Bulto>().OrderBy(p => p.Peso).Select(p => p.Peso).ToArrayAsync());
    }

    [PostgresFact]
    public async Task NoSeAceptaEscrituraDeHijoSinVersionDeSuAgregado()
    {
        var e = await GuardarEscenario();
        await using var writer = Context();
        var bulto = await writer.Set<Bulto>().SingleAsync();
        writer.Entry(bulto).Property(nameof(Bulto.Peso)).CurrentValue = 999m;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
        Assert.Contains("con seguimiento", error.Message);
        await using var reader = Context();
        Assert.Equal(e.Envio.Bultos.Single().Peso, await reader.Set<Bulto>().Select(p => p.Peso).SingleAsync());
    }

    [PostgresFact]
    public async Task MigracionAsignaVersionADatosExistentesSinCambiarElNegocio()
    {
        var e = await GuardarEscenario(recibido: true);
        Guid routeId;
        await using (var setup = Context())
        {
            var ruta = await NuevaRuta(setup);
            setup.Add(ruta); await setup.SaveChangesAsync(); routeId = ruta.Id;
            var migrator = setup.GetService<IMigrator>();
            await migrator.MigrateAsync("20261017182252_OperatorIsolation");
            await migrator.MigrateAsync();
        }
        await using var reader = Context();
        var envio = await reader.Envios.SingleAsync();
        var route = await reader.Set<Ruta>().SingleAsync();
        Assert.Equal(e.Envio.Id, envio.Id);
        Assert.Equal(routeId, route.Id);
        Assert.Equal(EstadoEnvio.EnDeposito, envio.Estado);
        Assert.NotEqual(Guid.Empty, reader.Entry(envio).Property<Guid>("RowVersion").CurrentValue);
        Assert.NotEqual(Guid.Empty, reader.Entry(route).Property<Guid>("RowVersion").CurrentValue);
    }

    private async Task<EscenarioPersistencia> GuardarEscenario(bool recibido = false)
    {
        var e = Escenario();
        if (recibido) e.Recibir();
        await using var db = Context();
        db.Add(e.Envio); await db.SaveChangesAsync();
        return e;
    }

    private static async Task<Ruta> NuevaRuta(AppDbContext db) => new(
        await db.Set<Repartidor>().Include(r => r.Operador).SingleAsync(),
        await db.Set<Vehiculo>().Include(v => v.Operador).SingleAsync(), 10);

    private static Task<Ruta> CargarRuta(AppDbContext db, Guid id) => db.Set<Ruta>().AsSplitQuery()
        .Include(r => r.Repartidor).ThenInclude(u => u.Operador)
        .Include(r => r.Vehiculo).ThenInclude(v => v.Operador)
        .Include(r => r.Paradas).ThenInclude(p => p.Envio).ThenInclude(e => e.Bultos)
        .SingleAsync(r => r.Id == id);
}
