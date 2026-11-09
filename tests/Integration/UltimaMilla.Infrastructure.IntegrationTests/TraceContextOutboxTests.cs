using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task OutboxConservaContextoDeCreacionAunqueCambieLaTrazaAlPublicarOReintentar()
    {
        string? traceParent;
        using (var peticion = new Activity("peticion-original").SetIdFormat(ActivityIdFormat.W3C).Start())
        {
            peticion.TraceStateString = "prueba=original";
            traceParent = peticion.Id;
            await GuardarEscenario();
        }
        Assert.NotNull(traceParent);

        await using var lectura = Context();
        var mensaje = await lectura.Set<MensajeOutbox>().SingleAsync();
        Assert.Equal(traceParent, mensaje.TraceParent);
        Assert.Equal("prueba=original", mensaje.TraceState);
        Assert.DoesNotContain("traceParent", mensaje.Contenido);
        Assert.DoesNotContain("traceState", mensaje.Contenido);

        using var publicacion = new Activity("otra-peticion").SetIdFormat(ActivityIdFormat.W3C).Start();
        Assert.NotEqual(traceParent, publicacion.Id);
        await using (var intento = Context())
        {
            await Assert.ThrowsAsync<IOException>(() => Publicador(intento, new TransportePrueba((salida, _) =>
            {
                Assert.Equal(mensaje.Id, salida.MensajeId);
                Assert.Equal(traceParent, salida.TraceParent);
                Assert.Equal("prueba=original", salida.TraceState);
                throw new IOException("Sin confirmación.");
            })).PublicarLoteAsync());
        }
        await using var siguiente = Context();
        Assert.Equal(1, await Publicador(siguiente, new TransportePrueba((salida, _) =>
        {
            Assert.Equal(traceParent, salida.TraceParent);
            Assert.Equal("prueba=original", salida.TraceState);
            Assert.Equal(mensaje.Contenido, salida.Contenido);
            return Task.CompletedTask;
        })).PublicarLoteAsync());
    }

    [PostgresFact]
    public async Task MigracionDeTrazasConservaMensajesAnterioresSinInventarSuContexto()
    {
        await GuardarEscenario();
        string contenido;
        Guid mensajeId;
        await using (var anterior = Context())
        {
            var mensaje = await anterior.Set<MensajeOutbox>().SingleAsync();
            contenido = mensaje.Contenido;
            mensajeId = mensaje.Id;
            var migrator = anterior.GetService<IMigrator>();
            await migrator.MigrateAsync("20261105204809_MensajeProcesado");
            await migrator.MigrateAsync();
        }

        await using var lectura = Context();
        var conservado = await lectura.Set<MensajeOutbox>().SingleAsync();
        Assert.Equal(mensajeId, conservado.Id);
        Assert.Equal(contenido, conservado.Contenido);
        Assert.Null(conservado.TraceParent);
        Assert.Null(conservado.TraceState);
        await using var publicacion = Context();
        Assert.Equal(1, await Publicador(publicacion, new TransportePrueba((salida, _) =>
        {
            Assert.Equal(mensajeId, salida.MensajeId);
            Assert.Null(salida.TraceParent);
            Assert.Null(salida.TraceState);
            return Task.CompletedTask;
        })).PublicarLoteAsync());
    }
}
