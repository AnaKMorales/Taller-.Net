using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Backoffice;
using UltimaMilla.Infrastructure.Persistence.Despacho;

namespace UltimaMilla.Infrastructure.IntegrationTests;

// Seguimiento periódico de la posición del vehículo (req. 23) contra PostgreSQL real.
public sealed partial class PostgresTests
{
    private static readonly DateTime Despacho = EscenarioPersistencia.Fecha;

    private sealed class RelojPosiciones(DateTime ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(ahora);
    }

    private async Task<(EscenarioPersistencia E, Ruta Ruta)> RutaDespachada()
    {
        var e = Escenario();
        await using var db = Context();
        db.Add(e.Envio);
        await db.SaveChangesAsync();
        e.Recibir();
        var ruta = e.Asignar();
        db.Add(ruta);
        ruta.Despachar(e.Admin, Despacho);
        await db.SaveChangesAsync();
        return (e, ruta);
    }

    private RegistrarPosicionesHandler Posiciones(EscenarioPersistencia e, Guid usuarioId, RolUsuario rol, DateTime ahora) =>
        new(new RegistroPosiciones(Options(), new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, usuarioId, rol)),
            new RelojPosiciones(ahora)));

    private static LecturaPosicionDto Lectura(Guid rutaId, int minutos, decimal latitud = -34.9m, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), rutaId, latitud, -56.16m, 7.5m, Despacho.AddMinutes(minutos));

    [PostgresFact]
    public async Task ElRepartidorRegistraSuRecorridoSinDuplicarAlReenviarElLote()
    {
        var (e, ruta) = await RutaDespachada();
        var handler = Posiciones(e, e.Repartidor.Id, RolUsuario.Repartidor, Despacho.AddHours(1));
        var lote = new[] { Lectura(ruta.Id, 10), Lectura(ruta.Id, 12, -34.91m) };
        object? versionAntes;
        await using (var antes = Context()) versionAntes = ConcurrencyValue(antes, ruta.Id);

        Assert.Equal(new PosicionesRegistradasDto(2, 0, 0), await handler.Handle(lote));
        // La cola offline del dispositivo reenvía el mismo lote tras perder la respuesta.
        Assert.Equal(new PosicionesRegistradasDto(0, 2, 0), await handler.Handle(lote));

        await using var db = Context();
        var guardadas = await db.Set<PosicionVehiculo>().AsNoTracking().OrderBy(p => p.RegistradaEn).ToListAsync();
        Assert.Equal(2, guardadas.Count);
        Assert.Equal(-34.91m, guardadas[1].Punto.Latitud);
        Assert.Equal(7.5m, guardadas[1].PrecisionMetros);
        Assert.Equal(DateTimeKind.Utc, guardadas[0].RegistradaEn.Kind);
        Assert.Equal(Despacho.AddHours(1), guardadas[0].RecibidaEn);
        // La lectura de GPS no cambia la versión de la ruta: no choca con el despachador.
        Assert.Equal(versionAntes, ConcurrencyValue(db, ruta.Id));
    }

    [PostgresFact]
    public async Task UnaLecturaInvalidaSeRechazaSinTirarElRestoDelLote()
    {
        var (e, ruta) = await RutaDespachada();
        var handler = Posiciones(e, e.Repartidor.Id, RolUsuario.Repartidor, Despacho.AddHours(1));

        var resultado = await handler.Handle([
            Lectura(ruta.Id, 15),
            Lectura(ruta.Id, -60),        // anterior al despacho
            Lectura(ruta.Id, 600),        // en el futuro
            Lectura(Guid.NewGuid(), 15)   // ruta inexistente
        ]);

        Assert.Equal(new PosicionesRegistradasDto(1, 0, 3), resultado);
    }

    [PostgresFact]
    public async Task NadieMasPuedeRegistrarEnLaRutaNiDesdeOtroOperador()
    {
        var (e, ruta) = await RutaDespachada();
        var ahora = Despacho.AddHours(1);
        // Otro repartidor del mismo operador: la ruta no es suya.
        Repartidor otro;
        await using (var db = Context())
        {
            otro = new Repartidor(await db.Set<Operador>().SingleAsync(), "otro.repartidor@example.com");
            db.Add(otro);
            await db.SaveChangesAsync();
        }
        Assert.Equal(new PosicionesRegistradasDto(0, 0, 1),
            await Posiciones(e, otro.Id, RolUsuario.Repartidor, ahora).Handle([Lectura(ruta.Id, 10)]));
        // Un rol que no es repartidor, o un tenant de otro operador, no puede usar el registro.
        await Assert.ThrowsAsync<AislamientoOperadorException>(() =>
            Posiciones(e, e.Admin.Id, RolUsuario.Administrador, ahora).Handle([Lectura(ruta.Id, 10)]));
        var ajeno = new RegistrarPosicionesHandler(new RegistroPosiciones(Options(), new TenantActual(
            TenantContext.ParaUsuario(Guid.NewGuid(), e.Repartidor.Id, RolUsuario.Repartidor)), new RelojPosiciones(ahora)));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => ajeno.Handle([Lectura(ruta.Id, 10)]));

        await using var lectura = Context();
        Assert.Empty(await lectura.Set<PosicionVehiculo>().ToListAsync());
    }

    [PostgresFact]
    public async Task ElMapaDeLaFlotaMuestraLaLecturaMasRecienteDelSeguimiento()
    {
        var (e, ruta) = await RutaDespachada();
        var ahora = Despacho.AddHours(2);
        await Posiciones(e, e.Repartidor.Id, RolUsuario.Repartidor, ahora)
            .Handle([Lectura(ruta.Id, 30, -34.80m), Lectura(ruta.Id, 90, -34.85m)]);

        await using (var db = Context())
        {
            var posicion = Assert.Single(await new ConsultasBackoffice(db, new RelojPosiciones(ahora))
                .UltimasPosicionesAsync(TimeSpan.FromHours(12)));
            Assert.Equal((e.Repartidor.Email, -34.85m, Despacho.AddMinutes(90), e.Vehiculo.Matricula),
                (posicion.Repartidor, posicion.Latitud, posicion.ReportadaEn, posicion.Vehiculo));
        }
        // Un intento de entrega posterior, con su propia posición, pasa a ser la última conocida.
        await using (var db = Context())
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            envio.RegistrarIntento(ResultadoIntento.Fallido, envio.Reglas.MotivosNoEntrega.Single(), [e.Foto(Despacho.AddMinutes(100))],
                await db.Set<Repartidor>().SingleAsync(), Despacho.AddMinutes(100), Guid.NewGuid(), new GeoPunto(-34.70m, -56.10m));
            await db.SaveChangesAsync();
        }
        await using (var db = Context())
        {
            var posicion = Assert.Single(await new ConsultasBackoffice(db, new RelojPosiciones(ahora))
                .UltimasPosicionesAsync(TimeSpan.FromHours(12)));
            Assert.Equal((-34.70m, Despacho.AddMinutes(100)), (posicion.Latitud, posicion.ReportadaEn));
        }
        // Otro operador no ve la flota.
        await using (var otro = Context(Guid.NewGuid()))
            Assert.Empty(await new ConsultasBackoffice(otro, new RelojPosiciones(ahora)).UltimasPosicionesAsync(TimeSpan.FromHours(12)));
    }

    private static object? ConcurrencyValue(AppDbContext db, Guid rutaId) =>
        db.Set<Ruta>().AsNoTracking().Where(r => r.Id == rutaId).Select(r => EF.Property<Guid>(r, "RowVersion")).Single();
}
