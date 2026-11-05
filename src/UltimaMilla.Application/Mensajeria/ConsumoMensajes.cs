using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Application.Mensajeria;

// Qué hacer con un mensaje ya procesado (o que no se pudo procesar).
public enum ResultadoConsumo
{
    Confirmar,   // ack: procesado o duplicado
    Reintentar,  // fallo transitorio: va a una cola de espera y vuelve más tarde
    Descartar,   // inválido o sin remedio: va a la cola de muertos
}

// Lo que el adaptador de RabbitMQ entrega, sin tipos del cliente AMQP.
public sealed record MensajeRecibido(string? MensajeId, string? Tipo, string Cuerpo, int Intentos);

public interface IManejadorMensajes
{
    Task<ResultadoConsumo> ManejarAsync(MensajeRecibido mensaje, CancellationToken cancellationToken = default);
}

// Acción de negocio sobre un evento de envío ya validado, con el tenant propio de ESE mensaje.
public interface IManejadorEventoEnvio
{
    Task ManejarAsync(EventoEnvioV1 evento, TenantContext tenant, CancellationToken cancellationToken = default);
}

// Descarte de duplicados por (MensajeId, Consumidor).
public interface IRegistroIdempotencia
{
    // Reserva la clave y ejecuta la acción de forma atómica. Devuelve false si ya se había procesado
    // (la acción no se ejecuta). Si la acción falla, la reserva se deshace y el reintento vuelve a intentar.
    Task<bool> EjecutarUnaVezAsync(TenantContext tenant, Guid mensajeId, string consumidor,
        Func<CancellationToken, Task> accion, CancellationToken cancellationToken = default);
}
