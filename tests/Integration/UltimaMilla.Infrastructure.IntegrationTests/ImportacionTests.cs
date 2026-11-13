using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Envios.Importacion;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Envios;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private ImportarEnviosHandler Importador(EscenarioPersistencia e)
    {
        var reloj = new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha));
        var tenant = new TenantActual(TenantContext.ParaUsuario(e.Operador.Id,
            e.Envio.CuentaComercial.Usuarios.Single().Id, RolUsuario.Comercio, e.Envio.CuentaComercial.Id));
        return new(new ImportacionEnvios(Options(), tenant, new TarifarioVigente(Options(), reloj), reloj, new()));
    }

    private async Task<FilaImportacion> Fila(EscenarioPersistencia e)
    {
        await using var db = ContextoComercio(e, e.Envio.CuentaComercial.Id);
        var solicitud = new SolicitudAltaPortal(e.Zona.Id, ModalidadServicio.Estandar, "Persona", "099123456",
            "persona@example.com", "Calle", "1", "Montevideo", "11000", [new BultoAlta("B-1", 1, 10, 10, 10)]);
        var cotizacion = await Handler(db, new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha))).CotizarAsync(solicitud);
        return new("PEDIDO-1", solicitud, cotizacion.Opciones[0].Clave);
    }

    [PostgresFact]
    public async Task ImportacionReenviaLotesParcialesDetectaCambiosYContinuaTrasFilaInvalida()
    {
        var e = Escenario();
        await using (var db = Context()) { db.Add(e.Envio); await db.SaveChangesAsync(); }
        var fila = await Fila(e);
        var handler = Importador(e);
        var lote = await handler.Handle([fila, fila with { Referencia = "MALA", ClaveEntrega = "invalida" },
            fila with { Referencia = "PEDIDO-2" }]);
        Assert.Equal([EstadoFilaImportacion.Creado, EstadoFilaImportacion.Error, EstadoFilaImportacion.Creado], lote.Select(r => r.Estado));
        var repeticion = Assert.Single(await Importador(e).Handle([fila]));
        Assert.Equal(EstadoFilaImportacion.Existente, repeticion.Estado);
        Assert.Equal(lote[0].Envio, repeticion.Envio);
        var conflicto = Assert.Single(await handler.Handle([fila with { Solicitud = fila.Solicitud with { Calle = "Otra" } }]));
        Assert.Equal(EstadoFilaImportacion.Conflicto, conflicto.Estado);
        await using var lectura = Context();
        Assert.Equal(3, await lectura.Envios.CountAsync());
        Assert.Equal(3, await lectura.Set<EventoDeEnvio>().CountAsync());
        Assert.Equal(3, await lectura.Set<UltimaMilla.Infrastructure.Persistence.Outbox.MensajeOutbox>().CountAsync());
    }

    [PostgresFact]
    public async Task DosImportacionesSimultaneasCreanUnSoloEnvio()
    {
        var e = Escenario();
        await using (var db = Context()) { db.Add(e.Envio); await db.SaveChangesAsync(); }
        var fila = await Fila(e);
        var resultados = await Task.WhenAll(Importador(e).Handle([fila]), Importador(e).Handle([fila]));
        Assert.Equal([EstadoFilaImportacion.Creado, EstadoFilaImportacion.Existente],
            resultados.SelectMany(r => r).Select(r => r.Estado).OrderBy(x => x));
        Assert.Single(resultados.SelectMany(r => r).Select(r => r.Envio!.CodigoSeguimiento).Distinct());
        await using var dbFinal = Context();
        Assert.Equal(2, await dbFinal.Envios.CountAsync());
    }

    [PostgresFact]
    public async Task FalloDelRegistroDeImportacionRevierteElAltaYPermiteReintentar()
    {
        var e = Escenario();
        await using (var db = Context())
        {
            db.Add(e.Envio); await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"AltasImportadas\" ADD CONSTRAINT \"BloqueoPrueba\" CHECK (false)");
        }
        var fila = await Fila(e);
        await Assert.ThrowsAsync<DbUpdateException>(() => Importador(e).Handle([fila]));
        await using (var db = Context())
        {
            Assert.Equal(1, await db.Envios.CountAsync());
            Assert.Equal(1, await db.Set<EventoDeEnvio>().CountAsync());
            Assert.Equal(1, await db.Set<UltimaMilla.Infrastructure.Persistence.Outbox.MensajeOutbox>().CountAsync());
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"AltasImportadas\" DROP CONSTRAINT \"BloqueoPrueba\"");
        }
        Assert.Equal(EstadoFilaImportacion.Creado, Assert.Single(await Importador(e).Handle([fila])).Estado);
    }

    [PostgresFact]
    public async Task ImportacionSeparaCuentasDelMismoOperadorYRepiteConFechaOriginalVencida()
    {
        var e = Escenario();
        var otraCuenta = new CuentaComercial(e.Operador, new Comercio("Otro", "099000009", "otro@example.com"));
        var otroUsuario = new Usuario(e.Operador, "otro-usuario@example.com", RolUsuario.Comercio);
        otroUsuario.AsignarCuentaComercial(otraCuenta);
        await using (var db = Context())
        { db.Add(e.Envio); db.Add(otraCuenta); await db.SaveChangesAsync(); }
        var fila = await Fila(e);
        var primero = Assert.Single(await Importador(e).Handle([fila]));
        var reloj = new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha));
        var otroTenant = new TenantActual(TenantContext.ParaUsuario(e.Operador.Id, otroUsuario.Id,
            RolUsuario.Comercio, otraCuenta.Id));
        var otroImportador = new ImportarEnviosHandler(new ImportacionEnvios(Options(), otroTenant,
            new TarifarioVigente(Options(), reloj), reloj, new()));
        var segundo = Assert.Single(await otroImportador.Handle([fila]));
        Assert.Equal(EstadoFilaImportacion.Creado, segundo.Estado);
        Assert.NotEqual(primero.Envio!.CodigoSeguimiento, segundo.Envio!.CodigoSeguimiento);
        // Un reenvío recupera la respuesta inicial aunque el reloj ya no ofrezca esa fecha.
        var relojPosterior = new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddDays(20)));
        var repetido = Assert.Single(await new ImportarEnviosHandler(new ImportacionEnvios(Options(), otroTenant,
            new TarifarioVigente(Options(), relojPosterior), relojPosterior, new())).Handle([fila]));
        Assert.Equal(EstadoFilaImportacion.Existente, repetido.Estado);
        Assert.Equal(segundo.Envio, repetido.Envio);
        await using var dbFinal = Context();
        Assert.Equal(3, await dbFinal.Envios.CountAsync());
    }

    [PostgresFact]
    public async Task LaReferenciaSeAislaPorOperadorYLaCuentaRevocadaNoPuedeReenviar()
    {
        var a = Escenario();
        var b = new EscenarioPersistencia();
        await using (var db = Context(a.Operador.Id)) { db.Add(a.Envio); await db.SaveChangesAsync(); }
        await using (var db = Context(b.Operador.Id)) { db.Add(b.Envio); await db.SaveChangesAsync(); }
        var filaA = await Fila(a);
        var filaB = await Fila(b);
        var resultadoA = Assert.Single(await Importador(a).Handle([filaA]));
        var resultadoB = Assert.Single(await Importador(b).Handle([filaB]));
        Assert.Equal(EstadoFilaImportacion.Creado, resultadoB.Estado);
        Assert.NotEqual(resultadoA.Envio!.CodigoSeguimiento, resultadoB.Envio!.CodigoSeguimiento);
        await using (var db = Context(a.Operador.Id))
        {
            var usuario = await db.Set<Usuario>().SingleAsync(u => u.Id == a.Envio.CuentaComercial.Usuarios.Single().Id);
            usuario.Desactivar();
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<UltimaMilla.Application.Abstractions.AislamientoOperadorException>(
            () => Importador(a).Handle([filaA]));
    }
}
