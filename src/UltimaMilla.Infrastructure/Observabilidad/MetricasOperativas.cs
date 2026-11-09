using System.Diagnostics.Metrics;

namespace UltimaMilla.Infrastructure.Observabilidad;

public static class MetricasOperativas
{
    public const string NombreMeter = "UltimaMilla.Operaciones";
    private static readonly Meter Meter = new(NombreMeter);
    private static readonly Counter<long> Publicados = Meter.CreateCounter<long>("ultimamilla.outbox.publicados");
    private static readonly Counter<long> FallosPublicacion = Meter.CreateCounter<long>("ultimamilla.outbox.errores");
    private static readonly Counter<long> Procesados = Meter.CreateCounter<long>("ultimamilla.mensajes.procesados");
    private static readonly Histogram<double> Duracion = Meter.CreateHistogram<double>("ultimamilla.mensajes.duracion", "s");
    private static readonly Gauge<long> Outbox = Meter.CreateGauge<long>("ultimamilla.outbox.pendientes");
    private static readonly Gauge<long> Avisos = Meter.CreateGauge<long>("ultimamilla.avisos.fallidos");
    private static readonly Gauge<long> Cola = Meter.CreateGauge<long>("ultimamilla.cola.profundidad");
    private static readonly Gauge<double> Muestra = Meter.CreateGauge<double>("ultimamilla.observabilidad.ultima_muestra", "s");

    public static void PublicacionConfirmada() => Publicados.Add(1);
    public static void PublicacionFallida() => FallosPublicacion.Add(1);
    public static void Procesamiento(string resultado, double segundos)
    {
        var tag = new KeyValuePair<string, object?>("resultado", resultado);
        Procesados.Add(1, tag);
        Duracion.Record(segundos, tag);
    }

    public static void EstadoBase(long pendientes, long fallidos)
    {
        Outbox.Record(pendientes);
        Avisos.Record(fallidos);
        Muestra.Record(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), new KeyValuePair<string, object?>("dependencia", "postgresql"));
    }

    public static void EstadoCola(string cola, long listos)
    {
        Cola.Record(listos, new KeyValuePair<string, object?>("cola", cola));
    }

    public static void ColasActualizadas()
    {
        Muestra.Record(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), new KeyValuePair<string, object?>("dependencia", "rabbitmq"));
    }
}
