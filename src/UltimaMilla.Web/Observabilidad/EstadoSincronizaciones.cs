using System.Diagnostics.Metrics;

namespace UltimaMilla.Web.Observabilidad;

public sealed class EstadoSincronizaciones(TimeProvider reloj) : IDisposable
{
    public const string NombreMeter = "UltimaMilla.Sincronizacion";
    private readonly Meter _meter = new(NombreMeter);
    private readonly Dictionary<(Guid Usuario, Guid Dispositivo), Muestra> _muestras = [];
    private readonly object _cerrojo = new();
    private bool _iniciado;

    public void Iniciar()
    {
        lock (_cerrojo)
        {
            if (_iniciado) return;
            _meter.CreateObservableGauge("ultimamilla.sincronizaciones.pendientes", () => Total(false));
            _meter.CreateObservableGauge("ultimamilla.sincronizaciones.rechazadas", () => Total(true));
            _meter.CreateObservableGauge("ultimamilla.sincronizaciones.dispositivos", () => TotalDispositivos());
            _iniciado = true;
        }
    }

    public bool Registrar(Guid usuario, Guid dispositivo, int pendientes, int rechazadas)
    {
        lock (_cerrojo)
        {
            Limpiar();
            var clave = (usuario, dispositivo);
            if (_muestras.Count >= 10000 && !_muestras.ContainsKey(clave)) return false;
            _muestras[clave] = new(pendientes, rechazadas, reloj.GetUtcNow());
            return true;
        }
    }

    private long Total(bool rechazadas)
    {
        lock (_cerrojo)
        {
            Limpiar();
            return _muestras.Values.Sum(m => (long)(rechazadas ? m.Rechazadas : m.Pendientes));
        }
    }

    private long TotalDispositivos()
    {
        lock (_cerrojo) { Limpiar(); return _muestras.Count; }
    }

    private void Limpiar()
    {
        var vencimiento = reloj.GetUtcNow().AddMinutes(-5);
        foreach (var clave in _muestras.Where(m => m.Value.Fecha <= vencimiento).Select(m => m.Key).ToArray())
            _muestras.Remove(clave);
    }

    public void Dispose() => _meter.Dispose();
    private sealed record Muestra(int Pendientes, int Rechazadas, DateTimeOffset Fecha);
}
