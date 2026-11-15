using UltimaMilla.Infrastructure.Persistence.Backoffice;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private static Envio EnvioParaReporte(Envio baseEnvio, CuentaComercial cuenta, Usuario admin, decimal tarifa) => new(cuenta,
        new Destinatario("Otra persona", "099111222", "p@example.com", new Direccion("Calle", "2", "Ciudad", "11000")),
        [new Bulto("OTRO", 1, 10, 10, 10)], baseEnvio.ModalidadServicio, baseEnvio.Zona,
        baseEnvio.Reglas, baseEnvio.CuadroTarifario, tarifa, baseEnvio.FechaAdmision, baseEnvio.FechaComprometida,
        ProgramacionEntrega.Crear(baseEnvio.Programacion.Franja, baseEnvio.Programacion.FechaEntrega,
            baseEnvio.Programacion.InicioLocal.Offset), admin);

    [PostgresFact]
    public async Task ReporteSeparaCuentasHomonimasSinMezclarImportesNiCumplimiento()
    {
        var e = Escenario();
        await GuardarConIntento(e, ResultadoIntento.Entregado, EscenarioPersistencia.Fecha.AddMinutes(30));
        await using (var db = Context(e.Operador.Id))
        {
            var original = await EscenarioPersistencia.Grafo(db).SingleAsync();
            var admin = await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id);
            var otraCuenta = new CuentaComercial(original.CuentaComercial.Operador,
                new Comercio(original.CuentaComercial.RazonSocial, "123", "otro@example.com"));
            db.Add(EnvioParaReporte(original, otraCuenta, admin, 200m));
            db.Add(EnvioParaReporte(original, original.CuentaComercial, admin, 50m));
            await db.SaveChangesAsync();
        }
        // Otro operador con el mismo nombre comercial no debe aportar filas ni importes.
        var ajeno = Escenario(); await GuardarConIntento(ajeno, ResultadoIntento.Fallido, EscenarioPersistencia.Fecha.AddHours(1));
        await using var lectura = Context(e.Operador.Id);
        var reporte = await new ReportesGestion(lectura).GenerarAsync(DiaEscenario, DiaEscenario, HoraUruguay);
        Assert.Equal(3, reporte.Resumen.Envios); Assert.Equal(1, reporte.Resumen.Entregados);
        Assert.Equal(400.25m, reporte.Resumen.Facturacion);
        Assert.Equal(2, reporte.PorComercio.Count); Assert.Equal(2, reporte.Liquidacion.Count);
        Assert.All(reporte.PorComercio, c => Assert.Equal("Comercio", c.Nombre));
        Assert.Equal((2, 1, 1, 0), (reporte.PorComercio[0].Envios, reporte.PorComercio[0].Entregados,
            reporte.PorComercio[0].EntregadosATiempo, reporte.PorComercio[0].IntentosFallidos));
        Assert.Equal((1, 0, 0, 0), (reporte.PorComercio[1].Envios, reporte.PorComercio[1].Entregados,
            reporte.PorComercio[1].EntregadosATiempo, reporte.PorComercio[1].IntentosFallidos));
        Assert.Equal(50, reporte.PorComercio[0].TasaEntrega);
        Assert.Equal(new[] { 200.25m, 200m }, reporte.Liquidacion.Select(l => l.Importe));
        Assert.Equal(new[] { 2, 1 }, reporte.Liquidacion.Select(l => l.Envios));
        Assert.Equal(reporte.Resumen.Facturacion, reporte.Liquidacion.Sum(l => l.Importe));
        Assert.Single(reporte.PorZona); // Sigue agrupándose como antes, no se duplica por cliente.
        var otro = await new ReportesGestion(lectura).GenerarAsync(DiaEscenario.AddDays(1), DiaEscenario.AddDays(1), HoraUruguay);
        Assert.Empty(otro.PorComercio); Assert.Empty(otro.Liquidacion);
        await using var dbAjeno = Context(ajeno.Operador.Id);
        var reporteAjeno = await new ReportesGestion(dbAjeno).GenerarAsync(DiaEscenario, DiaEscenario, HoraUruguay);
        Assert.Single(reporteAjeno.PorComercio); Assert.Single(reporteAjeno.Liquidacion);
        Assert.Equal(150.25m, reporteAjeno.Resumen.Facturacion);
    }

    [PostgresFact]
    public async Task ReporteConservaSeparacionAlRenombrarClienteYTrasSuBajaLogica()
    {
        var e = await PrepararAdministracionFlota();
        Guid id;
        await using (var db = Context())
        {
            var original = await EscenarioPersistencia.Grafo(db).SingleAsync();
            var admin = await db.Set<Usuario>().SingleAsync(u => u.Id == e.Admin.Id);
            var cuenta = new CuentaComercial(original.CuentaComercial.Operador, new Comercio("Distinto", "123", "d@example.com"));
            id = cuenta.Id; db.Add(EnvioParaReporte(original, cuenta, admin, 25m)); await db.SaveChangesAsync();
        }
        await using (var db = Context())
        {
            var cuenta = await db.Set<CuentaComercial>().SingleAsync(c => c.Id == id);
            cuenta.ModificarDatos("Comercio", "123", "d@example.com"); await db.SaveChangesAsync();
        }
        await using (var db = Context())
        {
            var reporte = await new ReportesGestion(db).GenerarAsync(DiaEscenario, DiaEscenario, HoraUruguay);
            Assert.Equal(2, reporte.PorComercio.Count); Assert.All(reporte.PorComercio, c => Assert.Equal("Comercio", c.Nombre));
            (await db.Set<CuentaComercial>().SingleAsync(c => c.Id == id)).DarBaja(); await db.SaveChangesAsync();
        }
        await using var lectura = Context();
        var final = await new ReportesGestion(lectura).GenerarAsync(DiaEscenario, DiaEscenario, HoraUruguay);
        Assert.Equal(2, final.PorComercio.Count); Assert.Equal(2, final.Liquidacion.Count);
        Assert.Equal(new[] { 150.25m, 25m }, final.Liquidacion.Select(c => c.Importe));
        Assert.Equal(175.25m, final.Resumen.Facturacion);
    }
}
