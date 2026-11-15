using UltimaMilla.Application.Portal;

namespace UltimaMilla.Infrastructure.Persistence.Backoffice;

// Cuenta corriente y liquidaciones del comercio. Los filtros globales (CuentaOwnership) dejan solo Envio,
// RecargoVisita e IntentoEntrega de la cuenta comercial de la sesión; fuera de una sesión de comercio no se devuelve
// nada. Los importes son los guardados (tarifa al alta y recargos persistidos).
public sealed class ConsultasCuentaPortal(AppDbContext db) : IConsultasCuentaPortal
{
    public async Task<CuentaCorrienteDto> CuentaCorrienteAsync(DateOnly desde, DateOnly hasta, TimeSpan desfaseHorario,
        CancellationToken cancellationToken = default)
    {
        var (desdeUtc, hastaUtc) = CuentaPortal.Rango(desde, hasta, desfaseHorario);
        if (!db.CuentaRestringida) return new(desde, hasta, []);
        return CuentaPortal.Cuenta(desde, hasta, await Envios(desdeUtc, hastaUtc, cancellationToken));
    }

    public async Task<IReadOnlyList<LiquidacionMesDto>> LiquidacionesAsync(int anio, TimeSpan desfaseHorario,
        CancellationToken cancellationToken = default)
    {
        var (desdeUtc, hastaUtc) = CuentaPortal.RangoAnio(anio, desfaseHorario);
        if (!db.CuentaRestringida) return [];
        var envios = await Envios(desdeUtc, hastaUtc, cancellationToken);
        // Motivo del intento fallido, en el mes de admisión del envío (igual que el reporte del operador).
        var motivos = await db.Set<IntentoEntrega>().AsNoTracking()
            .Where(i => i.Resultado == ResultadoIntento.Fallido && i.MotivoNoEntrega != null &&
                        i.Envio.FechaAdmision >= desdeUtc && i.Envio.FechaAdmision < hastaUtc)
            .Select(i => new CuentaPortal.FilaMotivo(i.Envio.FechaAdmision, i.MotivoNoEntrega!.Nombre))
            .ToListAsync(cancellationToken);
        return CuentaPortal.Liquidaciones(envios, motivos, desfaseHorario);
    }

    private Task<List<CuentaPortal.FilaEnvio>> Envios(DateTime desdeUtc, DateTime hastaUtc, CancellationToken cancellationToken) =>
        db.Envios.AsNoTracking()
            .Where(e => e.FechaAdmision >= desdeUtc && e.FechaAdmision < hastaUtc)
            .Select(e => new CuentaPortal.FilaEnvio(e.CodigoSeguimiento, e.FechaAdmision, e.Estado, e.TarifaCalculada,
                e.Recargos.Sum(r => (decimal?)r.Importe) ?? 0))
            .ToListAsync(cancellationToken);
}
