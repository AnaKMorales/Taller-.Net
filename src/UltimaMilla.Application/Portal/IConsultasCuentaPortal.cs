using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Portal;

// Cuenta corriente y liquidaciones del comercio, de solo lectura (docs/portal-cuenta-comercio.md). Las devoluciones
// del portal están en IConsultasPortal. Todo se deriva de lo que el sistema ya guarda: la tarifa calculada al alta y
// los recargos de visita persistidos. Nada se recalcula con el tarifario actual.

public sealed record MovimientoCuentaDto(string CodigoSeguimiento, DateTime FechaAdmision, EstadoEnvio Estado,
    decimal Tarifa, decimal Recargos)
{
    public decimal Importe => Tarifa + Recargos;
}

public sealed record CuentaCorrienteDto(DateOnly Desde, DateOnly Hasta, IReadOnlyList<MovimientoCuentaDto> Movimientos)
{
    public decimal TotalTarifas => Movimientos.Sum(m => m.Tarifa);
    public decimal TotalRecargos => Movimientos.Sum(m => m.Recargos);
    public decimal Total => Movimientos.Sum(m => m.Importe);
}

public sealed record MotivoLiquidacionDto(string Motivo, int Cantidad);

public sealed record LiquidacionMesDto(int Anio, int Mes, int Envios, decimal Tarifas, decimal Recargos,
    IReadOnlyList<MotivoLiquidacionDto> MotivosNoEntrega)
{
    public decimal Total => Tarifas + Recargos;
}

// Solo datos de la cuenta comercial del usuario: fuera de una sesión de comercio devuelve vacío.
public interface IConsultasCuentaPortal
{
    // Envíos admitidos entre desde y hasta (ambos días incluidos, hora local según el desfase).
    Task<CuentaCorrienteDto> CuentaCorrienteAsync(DateOnly desde, DateOnly hasta, TimeSpan desfaseHorario,
        CancellationToken cancellationToken = default);

    // Un resumen por cada mes del año que tenga envíos admitidos.
    Task<IReadOnlyList<LiquidacionMesDto>> LiquidacionesAsync(int anio, TimeSpan desfaseHorario,
        CancellationToken cancellationToken = default);
}

// Cálculo puro (sin base de datos): rangos, corte de mes y agrupación. Lo usa la implementación de Infrastructure.
public static class CuentaPortal
{
    public const int MaximoDias = 366;

    public sealed record FilaEnvio(string CodigoSeguimiento, DateTime FechaAdmision, EstadoEnvio Estado,
        decimal Tarifa, decimal Recargos);

    public sealed record FilaMotivo(DateTime FechaAdmision, string Motivo);

    // [desde 00:00 local, hasta+1 00:00 local), expresado en UTC como se guarda FechaAdmision.
    public static (DateTime DesdeUtc, DateTime HastaUtc) Rango(DateOnly desde, DateOnly hasta, TimeSpan desfaseHorario)
    {
        if (hasta < desde) throw new ArgumentException("El período termina antes de empezar.", nameof(hasta));
        if (hasta.DayNumber - desde.DayNumber + 1 > MaximoDias)
            throw new ArgumentException($"El período no puede superar {MaximoDias} días.", nameof(hasta));
        return (AUtc(desde, desfaseHorario), AUtc(hasta.AddDays(1), desfaseHorario));
    }

    public static (DateTime DesdeUtc, DateTime HastaUtc) RangoAnio(int anio, TimeSpan desfaseHorario)
    {
        if (anio is < 2000 or > 9998) throw new ArgumentOutOfRangeException(nameof(anio));
        return (AUtc(new DateOnly(anio, 1, 1), desfaseHorario), AUtc(new DateOnly(anio + 1, 1, 1), desfaseHorario));
    }

    // Día local de la admisión: decide a qué mes y período pertenece el envío.
    public static DateOnly DiaLocal(DateTime fechaAdmisionUtc, TimeSpan desfaseHorario) =>
        DateOnly.FromDateTime(fechaAdmisionUtc + desfaseHorario);

    public static CuentaCorrienteDto Cuenta(DateOnly desde, DateOnly hasta, IEnumerable<FilaEnvio> filas) =>
        new(desde, hasta, filas.OrderBy(f => f.FechaAdmision).ThenBy(f => f.CodigoSeguimiento, StringComparer.Ordinal)
            .Select(f => new MovimientoCuentaDto(f.CodigoSeguimiento, f.FechaAdmision, f.Estado, f.Tarifa, f.Recargos))
            .ToList());

    public static IReadOnlyList<LiquidacionMesDto> Liquidaciones(IEnumerable<FilaEnvio> envios,
        IEnumerable<FilaMotivo> motivos, TimeSpan desfaseHorario)
    {
        var motivosPorMes = motivos.GroupBy(m => Mes(m.FechaAdmision, desfaseHorario))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<MotivoLiquidacionDto>)g.GroupBy(m => m.Motivo)
                .Select(x => new MotivoLiquidacionDto(x.Key, x.Count()))
                .OrderByDescending(x => x.Cantidad).ThenBy(x => x.Motivo, StringComparer.Ordinal).ToList());
        return envios.GroupBy(e => Mes(e.FechaAdmision, desfaseHorario))
            .OrderBy(g => g.Key)
            .Select(g => new LiquidacionMesDto(g.Key.Anio, g.Key.Mes, g.Count(), g.Sum(e => e.Tarifa), g.Sum(e => e.Recargos),
                motivosPorMes.GetValueOrDefault(g.Key, [])))
            .ToList();
    }

    private static (int Anio, int Mes) Mes(DateTime fechaAdmisionUtc, TimeSpan desfaseHorario)
    {
        var dia = DiaLocal(fechaAdmisionUtc, desfaseHorario);
        return (dia.Year, dia.Month);
    }

    private static DateTime AUtc(DateOnly dia, TimeSpan desfaseHorario) =>
        DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.MinValue) - desfaseHorario, DateTimeKind.Utc);
}
