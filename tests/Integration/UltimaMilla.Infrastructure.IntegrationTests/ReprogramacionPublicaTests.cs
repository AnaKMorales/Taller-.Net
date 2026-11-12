using UltimaMilla.Application.Envios.ReprogramacionPublica;
using UltimaMilla.Infrastructure.Persistence.Envios;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task ElDestinatarioReprogramaConSuTokenYElEventoQuedaSinUsuario()
    {
        var e = Escenario(reintentable: true);
        await using (var db = Context())
        {
            db.Add(e.Envio);
            await db.SaveChangesAsync();
            e.Recibir();
            var ruta = e.Asignar();
            db.Add(ruta);
            ruta.Despachar(e.Admin, EscenarioPersistencia.Fecha);
            e.Envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reglas.MotivosNoEntrega.Single(), [e.Foto()], e.Repartidor,
                EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid(), new GeoPunto(-34.9m, -56.16m));
            await db.SaveChangesAsync();
        }
        var token = e.Envio.CodigoSeguimiento;
        var reloj = new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(3)));

        await using (var repositorio = new ReprogramacionPublicaRepository(Options()))
        {
            var handler = new ReprogramarPorDestinatarioHandler(repositorio, reloj);
            var opciones = await handler.OpcionesAsync(token);
            Assert.NotNull(opciones);
            Assert.True(opciones.Disponible);
            Assert.Equal(new DateOnly(2026, 10, 15), opciones.Opciones[0].Fecha); // desde mañana

            Assert.Equal((ResultadoReprogramacion.OpcionInvalida, "La fecha elegida ya no está disponible."),
                await handler.ReprogramarAsync(token, "2020-01-01|" + Guid.NewGuid()));
            Assert.Equal(ResultadoReprogramacion.Reprogramado, (await handler.ReprogramarAsync(token, opciones.Opciones[0].Clave)).Resultado);
        }

        await using (var db = Context())
        {
            var envio = await db.Envios.Include(x => x.Programacion).Include(x => x.Eventos).ThenInclude(v => v.Responsable)
                .SingleAsync(x => x.Id == e.Envio.Id);
            Assert.Equal(EstadoEnvio.Reprogramado, envio.Estado);
            Assert.Equal(new DateOnly(2026, 10, 15), envio.Programacion.FechaEntrega);
            var evento = envio.Eventos.OrderBy(v => v.OcurridoEn).Last();
            Assert.Equal((TipoEvento.Reintentar, OrigenEvento.Destinatario), (evento.Tipo, evento.Origen));
            Assert.Null(evento.Responsable);
        }

        // Ya reprogramado: no se puede volver a pedir. Un token inexistente o mal formado no encuentra nada.
        await using (var repositorio = new ReprogramacionPublicaRepository(Options()))
        {
            var handler = new ReprogramarPorDestinatarioHandler(repositorio, reloj);
            Assert.False((await handler.OpcionesAsync(token))!.Disponible);
            Assert.Null(await handler.OpcionesAsync(Guid.NewGuid().ToString("N")));
            Assert.Equal(ResultadoReprogramacion.NoEncontrado, (await handler.ReprogramarAsync("no-es-un-token", "x")).Resultado);
        }

        // La base también impide un evento sin usuario que no sea del destinatario.
        await using (var db = Context())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "UPDATE \"EventosDeEnvio\" SET \"ResponsableId\" = NULL WHERE \"Origen\" <> 3"));
            Assert.Equal("CK_EventosDeEnvio_ResponsableSegunOrigen", error.ConstraintName);
        }
    }
}
