using System.Text;
using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Infrastructure.Mensajeria;

public enum AccionFinal { Confirmar, EnviarAEspera, EnviarAMuertos }

// Qué hacer con el mensaje original y a dónde va. Sin RabbitMQ: pura lógica, fácil de probar.
public sealed record DecisionReintento(AccionFinal Accion, string? ColaEspera, int IntentosNuevos, ResultadoConsumo Efectivo);

public static class PoliticaReintentos
{
    public static DecisionReintento Decidir(ResultadoConsumo resultado, int intentosPrevios, OpcionesRabbitMq opciones)
    {
        ArgumentNullException.ThrowIfNull(opciones);
        var intentos = Math.Max(0, intentosPrevios);
        switch (resultado)
        {
            case ResultadoConsumo.Confirmar:
                return new(AccionFinal.Confirmar, null, intentos, resultado);
            case ResultadoConsumo.Reintentar when intentos < opciones.Esperas.Count:
                // 0 -> 5s, 1 -> 30s, 2 -> 2m
                return new(AccionFinal.EnviarAEspera, opciones.ColaEspera(opciones.Esperas[intentos].Sufijo), intentos + 1, resultado);
            case ResultadoConsumo.Reintentar:
                // Agotadas las esperas: a la cola de muertos.
                return new(AccionFinal.EnviarAMuertos, null, intentos, ResultadoConsumo.Descartar);
            default:
                return new(AccionFinal.EnviarAMuertos, null, intentos, ResultadoConsumo.Descartar);
        }
    }

    // El contador viaja en el encabezado x-intentos; el broker puede entregarlo como int, long, texto o bytes.
    public static int LeerIntentos(IDictionary<string, object?>? encabezados)
    {
        if (encabezados is null || !encabezados.TryGetValue(ConsumidorRabbitMq.EncabezadoIntentos, out var valor) || valor is null)
            return 0;
        var intentos = valor switch
        {
            int i => i,
            long l => (int)Math.Clamp(l, int.MinValue, int.MaxValue),
            short s => s,
            byte b => b,
            byte[] bytes => int.TryParse(Encoding.UTF8.GetString(bytes), out var n) ? n : 0,
            string texto => int.TryParse(texto, out var n) ? n : 0,
            _ => 0,
        };
        return Math.Max(0, intentos);
    }
}
