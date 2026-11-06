using UltimaMilla.Application.Backoffice;

namespace UltimaMilla.Infrastructure.Persistence.Backoffice;

// Una consulta trae una fila por envío y la agrupación se hace en memoria. Alcanza para el volumen de un
// operador en un período; si crece, el worker puede precalcular los indicadores (alcance, sección 4).
public sealed class ReportesGestion(AppDbContext db) : IReportesGestion
{
    public const string SinRepartidor = "Sin asignar";

    public async Task<ReporteGestion> GenerarAsync(DateOnly desde, DateOnly hasta, TimeSpan desfaseHorario,
        CancellationToken cancellationToken = default)
    {
        if (hasta < desde) throw new ArgumentException("El período termina antes de empezar.", nameof(hasta));
        var desdeUtc = AUtc(desde, desfaseHorario);
        var hastaUtc = AUtc(hasta.AddDays(1), desfaseHorario);
        var envios = db.Envios.AsNoTracking().Where(e => e.FechaAdmision >= desdeUtc && e.FechaAdmision < hastaUtc);

        var filas = await envios
            .Select(e => new Fila(
                e.Zona.Nombre,
                e.CuentaComercial.Comercio.RazonSocial,
                // El repartidor del último intento; si todavía no hubo intentos, el de la ruta asignada.
                e.Intentos.OrderByDescending(i => i.Numero).Select(i => i.Parada.Ruta.Repartidor.Email).FirstOrDefault()
                    ?? (e.Parada != null ? e.Parada.Ruta.Repartidor.Email : null),
                e.FechaAdmision,
                e.TarifaCalculada,
                e.Intentos.Any(i => i.Resultado == ResultadoIntento.Entregado),
                e.Intentos.Any(i => i.Resultado == ResultadoIntento.Entregado && i.OcurridoEn <= e.FechaComprometida),
                e.Intentos.Any(i => i.Resultado == ResultadoIntento.Entregado && i.Numero == 1),
                e.Intentos.Count(i => i.Resultado == ResultadoIntento.Fallido)))
            .ToListAsync(cancellationToken);

        var motivos = await db.Set<IntentoEntrega>().AsNoTracking()
            .Where(i => i.Resultado == ResultadoIntento.Fallido &&
                        i.Envio.FechaAdmision >= desdeUtc && i.Envio.FechaAdmision < hastaUtc)
            .GroupBy(i => i.MotivoNoEntrega!.Nombre)
            .OrderByDescending(g => g.Count())
            .Select(g => new MotivoNoEntregaReporte(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var resumen = new ResumenGestion(filas.Count, filas.Count(f => f.Entregado),
            filas.Count(f => f.PrimerIntento), filas.Count(f => f.ATiempo), filas.Sum(f => f.Tarifa));

        var volumen = filas
            .GroupBy(f => DateOnly.FromDateTime(f.FechaAdmision + desfaseHorario))
            .Select(g => new VolumenDia(g.Key, g.Count()))
            .OrderBy(v => v.Dia)
            .ToList();

        var liquidacion = filas
            .GroupBy(f => f.Comercio)
            .Select(g => new LiquidacionComercio(g.Key, g.Count(), g.Sum(f => f.Tarifa)))
            .OrderByDescending(l => l.Importe)
            .ToList();

        return new ReporteGestion(resumen, volumen,
            CumplimientoPor(filas, f => f.Zona),
            CumplimientoPor(filas, f => f.Repartidor ?? SinRepartidor),
            CumplimientoPor(filas, f => f.Comercio),
            motivos, liquidacion);
    }

    private static List<Cumplimiento> CumplimientoPor(List<Fila> filas, Func<Fila, string> clave) =>
        filas.GroupBy(clave)
            .Select(g => new Cumplimiento(g.Key, g.Count(), g.Count(f => f.Entregado), g.Count(f => f.ATiempo),
                g.Sum(f => f.IntentosFallidos)))
            .OrderByDescending(c => c.Envios)
            .ToList();

    // Medianoche local del día, expresada en UTC como se guarda en la base.
    private static DateTime AUtc(DateOnly dia, TimeSpan desfaseHorario) =>
        DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.MinValue) - desfaseHorario, DateTimeKind.Utc);

    private sealed record Fila(string Zona, string Comercio, string? Repartidor, DateTime FechaAdmision, decimal Tarifa,
        bool Entregado, bool ATiempo, bool PrimerIntento, int IntentosFallidos);
}
