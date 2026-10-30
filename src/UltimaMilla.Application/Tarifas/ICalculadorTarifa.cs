using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tarifas;

public interface ICalculadorTarifa
{
    // El servidor obtiene el tarifario con ITarifarioVigente para su operador autorizado.
    // No aceptar un tarifario ni el resultado del cálculo proporcionados por el navegador.
    TarifaCalculada Calcular(TarifarioDto tarifario, Guid zonaId, ModalidadServicio modalidad,
        IReadOnlyCollection<BultoAlta> bultos);
}

public sealed record TarifaCalculada(Guid CuadroId, int Version, decimal PesoTotalKg,
    decimal VolumenTotalM3, DesgloseTarifa Desglose);
