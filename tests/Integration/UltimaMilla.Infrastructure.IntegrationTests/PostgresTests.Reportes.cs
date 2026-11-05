using UltimaMilla.Application.Backoffice;
using UltimaMilla.Infrastructure.Persistence.Backoffice;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private static readonly DateOnly DiaEscenario = new(2026, 10, 14);
    private static readonly TimeSpan HoraUruguay = TimeSpan.FromHours(-3);

    [PostgresFact]
    public async Task ReporteDeGestionCalculaCumplimientoLiquidacionYMotivosDelOperador()
    {
        var entregado = Escenario();
        await GuardarConIntento(entregado, ResultadoIntento.Entregado, EscenarioPersistencia.Fecha.AddMinutes(30));
        var fallido = Escenario(reintentable: true);
        await GuardarConIntento(fallido, ResultadoIntento.Fallido, EscenarioPersistencia.Fecha.AddHours(1));

        await using (var db = Context(entregado.Operador.Id))
        {
            var reporte = await new ReportesGestion(db).GenerarAsync(DiaEscenario, DiaEscenario, HoraUruguay);
            Assert.Equal((1, 1, 1, 1, 150.25m), (reporte.Resumen.Envios, reporte.Resumen.Entregados,
                reporte.Resumen.EntregadosPrimerIntento, reporte.Resumen.EntregadosATiempo, reporte.Resumen.Facturacion));
            Assert.Equal(100, reporte.Resumen.TasaCumplimiento);
            Assert.Equal(new VolumenDia(DiaEscenario, 1), Assert.Single(reporte.VolumenPorDia));
            Assert.Equal(new Cumplimiento("Centro", 1, 1, 1, 0), Assert.Single(reporte.PorZona));
            Assert.Equal("repartidor@example.com", Assert.Single(reporte.PorRepartidor).Nombre);
            Assert.Equal(new LiquidacionComercio("Comercio", 1, 150.25m), Assert.Single(reporte.Liquidacion));
            Assert.Empty(reporte.MotivosNoEntrega);
        }

        // El otro operador solo ve su envío fallido.
        await using (var db = Context(fallido.Operador.Id))
        {
            var reporte = await new ReportesGestion(db).GenerarAsync(DiaEscenario, DiaEscenario, HoraUruguay);
            Assert.Equal((1, 0, 0), (reporte.Resumen.Envios, reporte.Resumen.Entregados, reporte.Resumen.EntregadosATiempo));
            Assert.Equal(new Cumplimiento("Comercio", 1, 0, 0, 1), Assert.Single(reporte.PorComercio));
            Assert.Equal(new MotivoNoEntregaReporte("Ausente o rechazo", 1), Assert.Single(reporte.MotivosNoEntrega));
        }

        // Fuera del período no hay nada: el envío entró el 14/10 a las 9:00 hora de Uruguay.
        await using (var db = Context(entregado.Operador.Id))
        {
            var reporte = await new ReportesGestion(db).GenerarAsync(DiaEscenario.AddDays(1), DiaEscenario.AddDays(7), HoraUruguay);
            Assert.Equal(0, reporte.Resumen.Envios);
            Assert.Empty(reporte.PorZona);
            Assert.Equal(0, reporte.Resumen.TasaEntrega);
        }
    }

    private async Task GuardarConIntento(EscenarioPersistencia e, ResultadoIntento resultado, DateTime momento)
    {
        await using var db = Context(e.Operador.Id);
        db.Add(e.Envio);
        e.Recibir();
        var ruta = e.Asignar();
        db.Add(ruta);
        ruta.Despachar(e.Admin, EscenarioPersistencia.Fecha);
        var motivo = resultado == ResultadoIntento.Fallido ? e.Reglas.MotivosNoEntrega.Single() : null;
        e.Envio.RegistrarIntento(resultado, motivo, [e.Foto(momento)], e.Repartidor, momento, Guid.NewGuid(),
            new GeoPunto(-34.9011m, -56.1645m));
        await db.SaveChangesAsync();
    }
}
