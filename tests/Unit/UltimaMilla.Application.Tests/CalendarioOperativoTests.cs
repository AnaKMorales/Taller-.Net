using UltimaMilla.Application.Backoffice;

namespace UltimaMilla.Application.Tests;

public sealed class CalendarioOperativoTests
{
    [Fact]
    public void CorteDiarioEsMedianocheUruguayYElBacklogRotaSinDejarDeRevisarHoyYAyer()
    {
        var reloj = new Reloj(new(2026, 11, 15, 2, 59, 59, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 11, 14), CalendarioOperativo.Hoy(reloj));
        reloj.Ahora = new(2026, 11, 15, 3, 0, 0, TimeSpan.Zero);
        var hoy = CalendarioOperativo.Hoy(reloj);
        Assert.Equal(new DateOnly(2026, 11, 15), hoy);
        var fechas = new Dictionary<DateOnly, DateTime> { [hoy.AddDays(-4)] = DateTime.UtcNow };
        var agenda = CalendarioOperativo.Planificar(hoy.AddDays(-4), hoy, 3, fechas);
        Assert.Equal(new[] { hoy, hoy.AddDays(-1), hoy.AddDays(-3) }, agenda);
        fechas[hoy.AddDays(-3)] = DateTime.UtcNow;
        Assert.Equal(hoy.AddDays(-2), CalendarioOperativo.Planificar(hoy.AddDays(-4), hoy, 3, fechas)[2]);
    }

    [Fact]
    public void OperadorNuevoIncluyeAyerVacioYNoGeneraDiasFuturos()
    {
        var hoy = new DateOnly(2026, 11, 15);
        Assert.Equal(new[] { hoy, hoy.AddDays(-1) }, CalendarioOperativo.Planificar(hoy.AddDays(2), hoy, 31, new Dictionary<DateOnly, DateTime>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => CalendarioOperativo.Planificar(hoy, hoy, 2, new Dictionary<DateOnly, DateTime>()));
    }
    private sealed class Reloj(DateTimeOffset fecha) : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = fecha;
        public override DateTimeOffset GetUtcNow() => Ahora;
    }
}
