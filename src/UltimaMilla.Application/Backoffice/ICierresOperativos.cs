namespace UltimaMilla.Application.Backoffice;

public sealed record IndicadoresDia(ReporteGestion EnviosAdmitidos, int RutasSinRendir, int DevolucionesAbiertas);
public sealed record CierreOperativoDto(DateOnly Dia, DateTime ActualizadoEn, DateTime? CerradoEn,
    int Recalculos, int VersionCalculo, IndicadoresDia Actual, IndicadoresDia? AlCierre);
public sealed record ResultadoRecalculo(int Actualizados, int Cerrados, int Omitidos);

// Puerto interno del Worker. Cada operador se procesa en un contexto nuevo y separado.
public interface IProcesamientoOperativo
{
    Task<IReadOnlyList<Guid>> ListarOperadoresAsync(CancellationToken ct = default);
    Task<ResultadoRecalculo> ProcesarAsync(Guid operadorId, int maximoDias, CancellationToken ct = default);
}

// Lectura administrativa: el operador se deriva de la identidad, nunca del pedido.
public interface ICierresOperativos
{
    Task<IReadOnlyList<CierreOperativoDto>> ConsultarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default);
}

public static class CalendarioOperativo
{
    public static readonly TimeSpan Desfase = TimeSpan.FromHours(-3);
    public static DateOnly Hoy(TimeProvider reloj) => DateOnly.FromDateTime(reloj.GetUtcNow().UtcDateTime + Desfase);

    // Hoy y ayer se revisan siempre; el resto rota para recuperar días omitidos y datos offline tardíos.
    public static IReadOnlyList<DateOnly> Planificar(DateOnly primeraFecha, DateOnly hoy, int maximo,
        IReadOnlyDictionary<DateOnly, DateTime> actualizaciones)
    {
        if (maximo is < 3 or > 366) throw new ArgumentOutOfRangeException(nameof(maximo));
        var ayer = hoy.AddDays(-1);
        if (primeraFecha > ayer) primeraFecha = ayer;
        return new[] { hoy, ayer }.Concat(Enumerable.Range(primeraFecha.DayNumber, hoy.DayNumber - primeraFecha.DayNumber)
                .Select(DateOnly.FromDayNumber).Where(d => d != ayer)
                .OrderBy(d => actualizaciones.GetValueOrDefault(d, DateTime.MinValue)).ThenBy(d => d))
            .Take(maximo).ToArray();
    }
}
