namespace UltimaMilla.Application.Backoffice;

// Cumplimiento: entregado a más tardar en la fecha comprometida.
public sealed record Cumplimiento(string Nombre, int Envios, int Entregados, int EntregadosATiempo, int IntentosFallidos)
{
    public double TasaEntrega => Porcentajes.De(Entregados, Envios);
    public double TasaCumplimiento => Porcentajes.De(EntregadosATiempo, Envios);
}

public sealed record ResumenGestion(int Envios, int Entregados, int EntregadosPrimerIntento, int EntregadosATiempo,
    decimal Facturacion)
{
    public double TasaEntrega => Porcentajes.De(Entregados, Envios);
    public double TasaCumplimiento => Porcentajes.De(EntregadosATiempo, Envios);
    public double TasaPrimerIntento => Porcentajes.De(EntregadosPrimerIntento, Entregados);
}

public sealed record VolumenDia(DateOnly Dia, int Envios);

public sealed record MotivoNoEntregaReporte(string Motivo, int Cantidad);

public sealed record LiquidacionComercio(string Comercio, int Envios, decimal Importe);

public sealed record ReporteGestion(ResumenGestion Resumen, IReadOnlyList<VolumenDia> VolumenPorDia,
    IReadOnlyList<Cumplimiento> PorZona, IReadOnlyList<Cumplimiento> PorRepartidor, IReadOnlyList<Cumplimiento> PorComercio,
    IReadOnlyList<MotivoNoEntregaReporte> MotivosNoEntrega, IReadOnlyList<LiquidacionComercio> Liquidacion);

// Envíos admitidos entre desde y hasta (ambos días incluidos, hora local), solo del operador del usuario.
public interface IReportesGestion
{
    Task<ReporteGestion> GenerarAsync(DateOnly desde, DateOnly hasta, TimeSpan desfaseHorario,
        CancellationToken cancellationToken = default);
}

internal static class Porcentajes
{
    internal static double De(int parte, int total) => total == 0 ? 0 : 100.0 * parte / total;
}
