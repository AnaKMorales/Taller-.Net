namespace UltimaMilla.Domain.Configuracion;

// Política pura: no consulta datos ni modifica versiones. Los tramos recibidos ya corresponden
// a la zona/modalidad elegidas; conserva [desde, hasta) y volumen máximo del modelo actual.
public static class CalculadoraTarifa
{
    public static DesgloseTarifa Calcular(decimal pesoTotalKg, decimal volumenTotalM3,
        IEnumerable<TramoCalculoTarifa> tramos)
    {
        Validacion.Positivo(pesoTotalKg, nameof(pesoTotalKg));
        Validacion.Positivo(volumenTotalM3, nameof(volumenTotalM3));
        ArgumentNullException.ThrowIfNull(tramos);
        var candidatos = new List<TramoCalculoTarifa>();
        foreach (var tramo in tramos)
        {
            ArgumentNullException.ThrowIfNull(tramo);
            if (tramo.PesoDesde < 0 || tramo.PesoHasta <= tramo.PesoDesde ||
                tramo.VolumenHasta <= 0 || tramo.Precio < 0)
                throw new ArgumentException("El tramo tarifario tiene límites o precio inválidos.", nameof(tramos));
            if (pesoTotalKg >= tramo.PesoDesde && pesoTotalKg < tramo.PesoHasta && volumenTotalM3 <= tramo.VolumenHasta)
                candidatos.Add(tramo);
        }
        if (candidatos.Count == 0)
            throw new InvalidOperationException("No hay una tarifa que cubra el peso y volumen del envío para la zona y modalidad seleccionadas.");
        if (candidatos.Count > 1)
            throw new InvalidOperationException("Más de una tarifa cubre el envío; debe corregirse la configuración ambigua.");

        var seleccionada = candidatos[0];
        ArgumentNullException.ThrowIfNull(seleccionada.Ajustes);
        decimal recargos = 0, bonificaciones = 0;
        foreach (var ajuste in seleccionada.Ajustes)
        {
            ArgumentNullException.ThrowIfNull(ajuste);
            Validacion.Enumeracion(ajuste.Tipo, nameof(ajuste.Tipo));
            Validacion.Positivo(ajuste.Porcentaje, nameof(ajuste.Porcentaje));
            if (ajuste.Tipo == TipoAjuste.Bonificacion && ajuste.Porcentaje > 100)
                throw new ArgumentException("Una bonificación no puede superar el 100%.", nameof(tramos));
            var importe = seleccionada.Precio * (ajuste.Porcentaje / 100m);
            if (ajuste.Tipo == TipoAjuste.Recargo) recargos += importe;
            else bonificaciones += importe;
        }
        var total = seleccionada.Precio + recargos - bonificaciones;
        if (total < 0)
            throw new InvalidOperationException("La combinación de ajustes produce una tarifa negativa.");
        return new(seleccionada.Precio, recargos, bonificaciones,
            decimal.Round(total, 2, MidpointRounding.AwayFromZero));
    }
}

// Valores de cálculo sin identidad ni navegaciones de persistencia.
public sealed record TramoCalculoTarifa(decimal PesoDesde, decimal PesoHasta, decimal VolumenHasta,
    decimal Precio, IReadOnlyCollection<AjusteCalculoTarifa> Ajustes);
public sealed record AjusteCalculoTarifa(TipoAjuste Tipo, decimal Porcentaje);
public sealed record DesgloseTarifa(decimal PrecioBase, decimal Recargos, decimal Bonificaciones, decimal Total);
