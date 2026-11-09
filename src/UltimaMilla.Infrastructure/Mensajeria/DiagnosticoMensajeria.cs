using System.Diagnostics;
using System.Text;

namespace UltimaMilla.Infrastructure.Mensajeria;

public static class DiagnosticoMensajeria
{
    public const string NombreFuente = "UltimaMilla.Mensajeria";
    internal static readonly ActivitySource Fuente = new(NombreFuente);

    internal static ActivityContext LeerContexto(IDictionary<string, object?>? headers)
    {
        var parent = Leer(headers, "traceparent", 55);
        var state = Leer(headers, "tracestate", 512);
        return ActivityContext.TryParse(parent, state, isRemote: true, out var context) ? context : default;
    }

    private static string? Leer(IDictionary<string, object?>? headers, string nombre, int maximo)
    {
        if (headers is null || !headers.TryGetValue(nombre, out var valor)) return null;
        return valor switch
        {
            string texto when texto.Length <= maximo => texto,
            byte[] bytes when bytes.Length <= maximo => Encoding.UTF8.GetString(bytes),
            _ => null
        };
    }
}
