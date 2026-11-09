using System.Text.Json;
using UltimaMilla.Application.Mensajeria;

namespace UltimaMilla.Application.Portal;

public enum ResultadoReenvio
{
    Reenviado,
    NoEncontrado,   // no existe o es de otra cuenta comercial
    YaEntregado,    // no hay nada que reenviar
    NoDisponible    // la cola no está disponible: el comercio puede volver a intentar
}

// El comercio reenvía un aviso que falló (sección 6.9) desde su panel.
// Se vuelve a publicar el evento en la cola con un MensajeId NUEVO: con el mismo, la idempotencia del
// Worker lo descartaría como duplicado. El Worker (EntregaWebhooks) solo envía a las suscripciones de ese
// evento que no tengan una entrega exitosa, así que reenviar nunca duplica un aviso ya entregado.
// No pasa por el outbox: el outbox garantiza que un cambio en la base y su evento se confirmen juntos,
// y el reenvío no cambia nada en la base. Si la cola no está, se informa y no queda nada a medias.
public sealed class ReenviarAvisoHandler(IPanelAvisos panel, ITransporteMensajes transporte, TimeProvider reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoReenvio> Handle(Guid avisoId, CancellationToken cancellationToken = default)
    {
        var aviso = await panel.AvisoParaReenviarAsync(avisoId, cancellationToken);
        if (aviso is null) return ResultadoReenvio.NoEncontrado;
        if (aviso.Entregado) return ResultadoReenvio.YaEntregado;

        var evento = aviso.Evento with { MensajeId = Guid.NewGuid() };
        var mensaje = new MensajeParaPublicar(evento.MensajeId, evento.OperadorId, EventoEnvioV1.TipoMensaje,
            JsonSerializer.Serialize(evento, Json), reloj.GetUtcNow().UtcDateTime);
        try
        {
            await transporte.PublicarConfirmadoAsync(mensaje, cancellationToken);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return ResultadoReenvio.NoDisponible;
        }
        return ResultadoReenvio.Reenviado;
    }
}
