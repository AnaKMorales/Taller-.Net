using UltimaMilla.Application.Portal;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Infrastructure.Persistence.Backoffice;

namespace UltimaMilla.Infrastructure.IntegrationTests;

// Devoluciones, cuenta corriente y liquidaciones del portal del comercio (docs/portal-cuenta-comercio.md).
public sealed partial class PostgresTests
{
    private static readonly TimeSpan HoraUruguayPortal = TimeSpan.FromHours(-3);
    private static readonly DateOnly OctubreDesde = new(2026, 10, 1), OctubreHasta = new(2026, 10, 31);

    private ConsultasCuentaPortal CuentaDe(Guid operador, Guid usuario, Guid cuenta) =>
        new(Scoped(TenantContext.ParaUsuario(operador, usuario, RolUsuario.Comercio, cuenta)));

    // Escenario con dos comercios del mismo operador: A con un envío fallido y devuelto; B con un envío propio.
    private async Task<(EscenarioPersistencia E, Usuario UsuarioB, CuentaComercial CuentaB, Envio EnvioB)> DosComerciosDelOperador()
    {
        var e = Escenario();
        var cuentaB = new CuentaComercial(e.Operador, new Comercio("Comercio B", "099000009", "b@example.com"));
        var usuarioB = new Usuario(e.Operador, "usuario-b@example.com", RolUsuario.Comercio);
        usuarioB.AsignarCuentaComercial(cuentaB);
        var envioB = new Envio(cuentaB, new Destinatario("Persona B", "099000003", "pb@example.com",
                new Direccion("Otra", "20", "Montevideo", "11200")), [new Bulto("B-1", 1, 10, 10, 10)],
            ModalidadServicio.Estandar, e.Zona, e.Reglas, e.Envio.CuadroTarifario, 99.90m,
            EscenarioPersistencia.Fecha.AddHours(2), EscenarioPersistencia.Fecha.AddHours(3),
            ProgramacionEntrega.Crear(e.Zona.Franjas.Single(), new(2026, 10, 14), HoraUruguayPortal), e.Admin);
        await using (var db = Context()) { db.AddRange(e.Envio, envioB, usuarioB); await db.SaveChangesAsync(); }
        // A: intento fallido con motivo y devolución completada.
        await CompletarFlujo(e);
        return (e, usuarioB, cuentaB, envioB);
    }

    [PostgresFact]
    public async Task PortalCuentaCadaComercioVeSoloSusEnviosDevolucionesYLiquidaciones()
    {
        var (e, usuarioB, cuentaB, envioB) = await DosComerciosDelOperador();
        var usuarioA = e.Envio.CuentaComercial.Usuarios.Single();
        var a = CuentaDe(e.Operador.Id, usuarioA.Id, e.Envio.CuentaComercialId);
        var b = CuentaDe(e.Operador.Id, usuarioB.Id, cuentaB.Id);

        // Devoluciones: solo las del comercio dueño, con el plazo de la política del envío (5 días) y su estado.
        var devolucion = Assert.Single(await a.DevolucionesAsync());
        Assert.Equal((e.Envio.CodigoSeguimiento, EstadoDevolucion.Completado, 1, 1),
            (devolucion.CodigoSeguimiento, devolucion.Estado, devolucion.Bultos, devolucion.DiasEnDeposito));
        Assert.Equal(devolucion.FechaInicio.AddDays(5), devolucion.PlazoLimite);
        Assert.NotNull(devolucion.FechaCierre);
        Assert.Empty(await b.DevolucionesAsync());

        // Cuenta corriente: cada uno ve solo sus envíos e importes.
        var cuentaA = await a.CuentaCorrienteAsync(OctubreDesde, OctubreHasta, HoraUruguayPortal);
        Assert.Equal([e.Envio.CodigoSeguimiento], cuentaA.Movimientos.Select(m => m.CodigoSeguimiento));
        Assert.Equal((150.25m, 0m, 150.25m), (cuentaA.TotalTarifas, cuentaA.TotalRecargos, cuentaA.Total));
        Assert.Equal(EstadoEnvio.Devuelto, cuentaA.Movimientos.Single().Estado);
        var cuentaB2 = await b.CuentaCorrienteAsync(OctubreDesde, OctubreHasta, HoraUruguayPortal);
        Assert.Equal([envioB.CodigoSeguimiento], cuentaB2.Movimientos.Select(m => m.CodigoSeguimiento));
        Assert.Equal(99.90m, cuentaB2.Total);

        // Liquidación: motivos de no entrega solo del propio comercio.
        var octubreA = Assert.Single(await a.LiquidacionesAsync(2026, HoraUruguayPortal));
        Assert.Equal((10, 1, 150.25m), (octubreA.Mes, octubreA.Envios, octubreA.Total));
        Assert.Equal([("Ausente o rechazo", 1)], octubreA.MotivosNoEntrega.Select(m => (m.Motivo, m.Cantidad)));
        var octubreB = Assert.Single(await b.LiquidacionesAsync(2026, HoraUruguayPortal));
        Assert.Equal((1, 99.90m), (octubreB.Envios, octubreB.Total));
        Assert.Empty(octubreB.MotivosNoEntrega);

        // Otro operador o el backoffice (sin cuenta): nada. (Que el usuario pertenezca a la cuenta de la sesión lo
        // valida TenantResolver al resolver los claims; tiene sus propias pruebas.)
        var otroOperador = CuentaDe(Guid.NewGuid(), usuarioA.Id, e.Envio.CuentaComercialId);
        Assert.Empty((await otroOperador.CuentaCorrienteAsync(OctubreDesde, OctubreHasta, HoraUruguayPortal)).Movimientos);
        Assert.Empty(await otroOperador.DevolucionesAsync());
        var backoffice = new ConsultasCuentaPortal(Scoped(TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador)));
        Assert.Empty(await backoffice.DevolucionesAsync());
        Assert.Empty((await backoffice.CuentaCorrienteAsync(OctubreDesde, OctubreHasta, HoraUruguayPortal)).Movimientos);
        Assert.Empty(await backoffice.LiquidacionesAsync(2026, HoraUruguayPortal));
        // Fuera del período: vacío.
        Assert.Empty((await a.CuentaCorrienteAsync(new(2026, 11, 1), new(2026, 11, 30), HoraUruguayPortal)).Movimientos);
    }

    [PostgresFact]
    public async Task PortalCuentaUnTarifarioNuevoNoAlteraLaLiquidacionPasada()
    {
        var e = Escenario();
        await using (var db = Context()) { db.Add(e.Envio); await db.SaveChangesAsync(); }
        var usuario = e.Envio.CuentaComercial.Usuarios.Single();
        var antes = Assert.Single(await CuentaDe(e.Operador.Id, usuario.Id, e.Envio.CuentaComercialId)
            .LiquidacionesAsync(2026, HoraUruguayPortal));
        Assert.Equal(150.25m, antes.Total);

        // El operador publica una versión nueva, mucho más cara, vigente desde antes del envío.
        await using (var db = Context())
        {
            var zona = await db.Set<ZonaCobertura>().SingleAsync();
            var nuevo = new CuadroTarifario(await db.Set<Operador>().SingleAsync(), 2, EscenarioPersistencia.Fecha.AddDays(-2));
            _ = new LineaTarifa(nuevo, zona, ModalidadServicio.Estandar, 0, 100, 10, 9999);
            db.Add(nuevo);
            await db.SaveChangesAsync();
        }

        var portal = CuentaDe(e.Operador.Id, usuario.Id, e.Envio.CuentaComercialId);
        var despues = Assert.Single(await portal.LiquidacionesAsync(2026, HoraUruguayPortal));
        Assert.Equal((antes.Envios, antes.Tarifas, antes.Recargos, antes.Total), (despues.Envios, despues.Tarifas, despues.Recargos, despues.Total));
        Assert.Equal(150.25m, (await portal.CuentaCorrienteAsync(OctubreDesde, OctubreHasta, HoraUruguayPortal)).Total);
    }

    [PostgresFact]
    public async Task PortalCuentaSumaElRecargoDeVisitaPersistido()
    {
        var (e, rutaId) = await PrepararVisitaConRecargo();
        await using (var db = Context(e.Operador.Id))
        {
            await DespacharVisita(db, rutaId, e.Admin.Id);
            await db.SaveChangesAsync();
        }
        var usuario = e.Envio.CuentaComercial.Usuarios.Single();
        var portal = CuentaDe(e.Operador.Id, usuario.Id, e.Envio.CuentaComercialId);

        var movimiento = Assert.Single((await portal.CuentaCorrienteAsync(OctubreDesde, OctubreHasta, HoraUruguayPortal)).Movimientos);
        Assert.Equal((150.25m, 25.50m, 175.75m), (movimiento.Tarifa, movimiento.Recargos, movimiento.Importe));
        Assert.Equal(175.75m, Assert.Single(await portal.LiquidacionesAsync(2026, HoraUruguayPortal)).Total);
    }
}
