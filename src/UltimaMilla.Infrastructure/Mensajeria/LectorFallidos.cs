using System.Text.Json;
using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Infrastructure.Mensajeria;

public static class LectorFallidos
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Devuelve null si el contenido no es un EventoEnvioV1 utilizable: sin operador no se puede decidir
    // quién puede verlo ni reintentarlo, así que no se muestra a nadie.
    public static (MensajeFallido Mensaje, Guid OperadorId)? Leer(string? mensajeId, string? tipo, string cuerpo,
        int intentos, DateTime? fallidoEn)
    {
        if (string.IsNullOrWhiteSpace(mensajeId)) return null;
        EventoEnvioV1? evento;
        try { evento = JsonSerializer.Deserialize<EventoEnvioV1>(cuerpo, Json); }
        catch (JsonException) { return null; }
        if (evento is null || evento.OperadorId == Guid.Empty) return null;
        return (new MensajeFallido(mensajeId, tipo, evento.EnvioId, evento.TipoEvento ?? "", evento.EstadoAnterior,
            evento.EstadoNuevo ?? "", evento.OcurridoEn, intentos, fallidoEn, cuerpo), evento.OperadorId);
    }
}
