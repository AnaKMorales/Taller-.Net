namespace UltimaMilla.Application.Mensajeria;

// Un mensaje que agotó sus reintentos (o se descartó) y está en avisos.dlq.
public sealed record MensajeFallido(string MensajeId, string? Tipo, Guid EnvioId, string TipoEvento,
    string? EstadoAnterior, string EstadoNuevo, DateTime OcurridoEn, int Intentos, DateTime? FallidoEn, string Cuerpo);

public interface IColaFallidos
{
    // false si RabbitMQ no está configurado en este host: la pantalla lo avisa en vez de fallar.
    bool Configurada { get; }

    // Solo devuelve los mensajes del operador indicado (el que viene del TenantContext verificado).
    // Un mensaje cuyo contenido no permite saber de qué operador es no se muestra a nadie.
    Task<IReadOnlyList<MensajeFallido>> ListarAsync(Guid operadorId, int maximo = 100, CancellationToken cancellationToken = default);

    // Devuelve el mensaje a "avisos" con el contador de intentos en cero. false si no existe en la DLQ
    // o no pertenece a ese operador.
    Task<bool> ReintentarAsync(Guid operadorId, string mensajeId, CancellationToken cancellationToken = default);
}
