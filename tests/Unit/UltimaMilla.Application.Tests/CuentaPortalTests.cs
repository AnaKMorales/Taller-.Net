using UltimaMilla.Application.Portal;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tests;

// Cálculo de cuenta corriente y liquidación del portal: totales, recargos y corte de mes en hora de Uruguay.
public sealed class CuentaPortalTests
{
    private static readonly TimeSpan Uruguay = TimeSpan.FromHours(-3);

    private static CuentaPortal.FilaEnvio Fila(string codigo, DateTime utc, decimal tarifa, decimal recargos = 0) =>
        new(codigo, utc, EstadoEnvio.Entregado, tarifa, recargos);

    private static DateTime Utc(int anio, int mes, int dia, int hora, int minuto = 0) =>
        new(anio, mes, dia, hora, minuto, 0, DateTimeKind.Utc);

    [Fact]
    public void CuentaSumaTarifasYRecargosYOrdenaPorFecha()
    {
        var cuenta = CuentaPortal.Cuenta(new(2026, 10, 1), new(2026, 10, 31),
        [
            Fila("B", Utc(2026, 10, 20, 15), 200m, 25.50m),
            Fila("A", Utc(2026, 10, 5, 15), 150.25m),
        ]);

        Assert.Equal(["A", "B"], cuenta.Movimientos.Select(m => m.CodigoSeguimiento));
        Assert.Equal(225.50m, cuenta.Movimientos[1].Importe);
        Assert.Equal((350.25m, 25.50m, 375.75m), (cuenta.TotalTarifas, cuenta.TotalRecargos, cuenta.Total));
    }

    [Fact]
    public void CuentaVaciaTotalizaCero()
    {
        var cuenta = CuentaPortal.Cuenta(new(2026, 10, 1), new(2026, 10, 31), []);
        Assert.Empty(cuenta.Movimientos);
        Assert.Equal(0m, cuenta.Total);
    }

    [Fact]
    public void ElRangoIncluyeLosDosDiasEnteros()
    {
        var (desde, hasta) = CuentaPortal.Rango(new(2026, 10, 1), new(2026, 10, 31), Uruguay);
        // 01/10 00:00 en Uruguay = 03:00 UTC; el fin es el 01/11 00:00 local, excluido.
        Assert.Equal(Utc(2026, 10, 1, 3), desde);
        Assert.Equal(Utc(2026, 11, 1, 3), hasta);
        Assert.Equal(DateTimeKind.Utc, desde.Kind);
    }

    [Fact]
    public void ElRangoRechazaPeriodosInvertidosOMuyLargos()
    {
        Assert.Throws<ArgumentException>(() => CuentaPortal.Rango(new(2026, 10, 2), new(2026, 10, 1), Uruguay));
        Assert.Throws<ArgumentException>(() => CuentaPortal.Rango(new(2025, 1, 1), new(2026, 1, 2), Uruguay));
        // Exactamente el máximo sí se acepta.
        CuentaPortal.Rango(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1).AddDays(CuentaPortal.MaximoDias - 1), Uruguay);
    }

    [Fact]
    public void ElMesSeCortaEnHoraDeUruguayNoEnUtc()
    {
        // 01/11 02:30 UTC es todavía 31/10 23:30 en Uruguay: va a octubre.
        var finDeOctubre = Utc(2026, 11, 1, 2, 30);
        // 01/11 03:00 UTC ya es 01/11 00:00 en Uruguay: va a noviembre.
        var inicioDeNoviembre = Utc(2026, 11, 1, 3);
        var meses = CuentaPortal.Liquidaciones([Fila("A", finDeOctubre, 100m), Fila("B", inicioDeNoviembre, 40m, 10m)], [], Uruguay);

        Assert.Equal([(2026, 10, 1, 100m), (2026, 11, 1, 50m)], meses.Select(m => (m.Anio, m.Mes, m.Envios, m.Total)));
        Assert.Equal(new DateOnly(2026, 10, 31), CuentaPortal.DiaLocal(finDeOctubre, Uruguay));
    }

    [Fact]
    public void LaLiquidacionAgrupaImportesYMotivosPorMesDeAdmision()
    {
        var octubre = Utc(2026, 10, 10, 12);
        var noviembre = Utc(2026, 11, 10, 12);
        var meses = CuentaPortal.Liquidaciones(
            [Fila("A", octubre, 100m, 20m), Fila("B", octubre, 50m), Fila("C", noviembre, 70m)],
            [new(octubre, "Ausente"), new(octubre, "Ausente"), new(octubre, "Dirección incorrecta"), new(noviembre, "Ausente")],
            Uruguay);

        var oct = meses[0];
        Assert.Equal((2, 150m, 20m, 170m), (oct.Envios, oct.Tarifas, oct.Recargos, oct.Total));
        Assert.Equal([("Ausente", 2), ("Dirección incorrecta", 1)], oct.MotivosNoEntrega.Select(m => (m.Motivo, m.Cantidad)));
        Assert.Equal([("Ausente", 1)], meses[1].MotivosNoEntrega.Select(m => (m.Motivo, m.Cantidad)));
    }

    [Fact]
    public void ElAnioTambienSeCortaEnHoraLocal()
    {
        var (desde, hasta) = CuentaPortal.RangoAnio(2026, Uruguay);
        Assert.Equal((Utc(2026, 1, 1, 3), Utc(2027, 1, 1, 3)), (desde, hasta));
        Assert.Throws<ArgumentOutOfRangeException>(() => CuentaPortal.RangoAnio(1999, Uruguay));
    }
}
