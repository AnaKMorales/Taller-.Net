using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public Task VehiculoNuevoYAsignacionSeGuardanJuntos() => GuardarFlotaYAsignacion(useAsync: true);

    [PostgresFact]
    public Task VehiculoNuevoYAsignacionSeGuardanJuntosSincronicamente() => GuardarFlotaYAsignacion(useAsync: false);

    private async Task GuardarFlotaYAsignacion(bool useAsync)
    {
        var e = await GuardarEscenario(recibido: true);
        Guid rutaId;
        Guid vehiculoId;
        await using (var db = Scoped(TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador)))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            var versionAnterior = db.Entry(envio).Property<Guid>("RowVersion").CurrentValue;
            var admin = await db.Set<Usuario>().SingleAsync(x => x.Id == e.Admin.Id);
            var vehiculo = new Vehiculo(await db.Set<Operador>().SingleAsync(), "NUEVO123", 100, 10);
            var ruta = new Ruta(await db.Set<Repartidor>().SingleAsync(), vehiculo, 10);
            ruta.AgregarParada(envio, 1, EscenarioPersistencia.Fecha.AddHours(1), admin, EscenarioPersistencia.Fecha);
            db.Add(ruta);
            if (useAsync) await db.SaveChangesAsync();
            else db.SaveChanges();
            Assert.NotEqual(versionAnterior, db.Entry(envio).Property<Guid>("RowVersion").CurrentValue);
            rutaId = ruta.Id;
            vehiculoId = vehiculo.Id;
        }

        await using var reader = Context();
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(EstadoEnvio.AsignadoARuta, actual.Estado);
        Assert.Equal(rutaId, actual.Parada!.RutaId);
        Assert.True(actual.Parada.Activa);
        var rutaActual = await CargarRuta(reader, rutaId);
        Assert.Equal(vehiculoId, rutaActual.Vehiculo.Id);
        Assert.Equal(e.Operador.Id, rutaActual.Vehiculo.OperadorId);
        Assert.Equal(new[] { e.Vehiculo.Id, vehiculoId }.Order(),
            (await reader.Set<Vehiculo>().Select(x => x.Id).ToListAsync()).Order());
        Assert.Equal(3, actual.Eventos.Count);
        var mensajes = await reader.Set<MensajeOutbox>().ToListAsync();
        Assert.Equal(actual.Eventos.Select(x => x.Id).Order(), mensajes.Select(x => x.Id).Order());
        Assert.All(mensajes, x => Assert.Null(x.PublicadoEn));
        foreach (var entity in new object[] { actual, actual.Parada, rutaActual, rutaActual.Vehiculo })
            Assert.Equal(e.Operador.Id, (Guid)reader.Entry(entity).Property("TenantId").CurrentValue!);

        await using var otro = Context(Guid.NewGuid());
        Assert.Empty(await otro.Set<Vehiculo>().ToListAsync());
        Assert.Empty(await otro.Set<Ruta>().ToListAsync());
        Assert.Empty(await otro.Set<Parada>().ToListAsync());
        Assert.Empty(await otro.Set<MensajeOutbox>().ToListAsync());
    }
}
