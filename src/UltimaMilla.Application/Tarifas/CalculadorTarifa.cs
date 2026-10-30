using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tarifas;

// Adaptador puro del DTO de lectura/caché a la política de negocio. Sin EF, Redis ni acceso a datos.
public sealed class CalculadorTarifa : ICalculadorTarifa
{
    public TarifaCalculada Calcular(TarifarioDto tarifario, Guid zonaId, ModalidadServicio modalidad,
        IReadOnlyCollection<BultoAlta> bultos)
    {
        ArgumentNullException.ThrowIfNull(tarifario);
        ArgumentNullException.ThrowIfNull(tarifario.Lineas);
        ArgumentNullException.ThrowIfNull(bultos);
        if (tarifario.CuadroId == Guid.Empty || tarifario.Version <= 0 || zonaId == Guid.Empty || !Enum.IsDefined(modalidad))
            throw new ArgumentException("La versión del tarifario, la zona y la modalidad deben ser válidas.");
        if (bultos.Count == 0) throw new ArgumentException("Se requiere al menos un bulto.", nameof(bultos));

        // Reutiliza las unidades y validaciones de Domain sin vincular ni guardar estos objetos.
        var medidas = bultos.Select(b => b is null
            ? throw new ArgumentException("No se admiten bultos nulos.", nameof(bultos))
            : new Bulto(b.Codigo, b.Peso, b.Alto, b.Ancho, b.Largo)).ToArray();
        if (medidas.Select(b => b.Codigo).Distinct(StringComparer.OrdinalIgnoreCase).Count() != medidas.Length)
            throw new ArgumentException("Los códigos de bulto no pueden repetirse.", nameof(bultos));
        var peso = medidas.Sum(b => b.Peso);
        var volumen = medidas.Sum(b => b.VolumenM3);
        var tramos = new List<TramoCalculoTarifa>();
        foreach (var linea in tarifario.Lineas)
        {
            ArgumentNullException.ThrowIfNull(linea);
            if (linea.ZonaId != zonaId || linea.Modalidad != modalidad) continue;
            ArgumentNullException.ThrowIfNull(linea.Ajustes);
            tramos.Add(new(linea.PesoDesde, linea.PesoHasta, linea.VolumenHasta, linea.Precio,
                linea.Ajustes.Select(a => a is null
                    ? throw new ArgumentException("No se admiten ajustes nulos.", nameof(tarifario))
                    : new AjusteCalculoTarifa(a.Tipo, a.ValorPorcentaje)).ToArray()));
        }
        return new(tarifario.CuadroId, tarifario.Version, peso, volumen,
            CalculadoraTarifa.Calcular(peso, volumen, tramos));
    }
}
