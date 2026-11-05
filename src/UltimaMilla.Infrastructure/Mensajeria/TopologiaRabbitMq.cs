using RabbitMQ.Client;

namespace UltimaMilla.Infrastructure.Mensajeria;

// Declaración idempotente: se puede ejecutar en cada arranque. Si una cola ya existe con otros
// argumentos RabbitMQ rechaza la declaración; en ese caso hay que migrar la cola a propósito.
//
//   ultimamilla.eventos (topic) --[envio.#]--> avisos --(rechazo)--> avisos.dlq
//   avisos.espera.5s / .30s / .2m : sin consumidores; al vencer el TTL el mensaje vuelve a "avisos".
public sealed class TopologiaRabbitMq(ConexionRabbitMq conexion, OpcionesRabbitMq opciones)
{
    public async Task DeclararAsync(CancellationToken cancellationToken = default)
    {
        opciones.Validar();
        var amqp = await conexion.ObtenerAsync(cancellationToken);
        await using var canal = await amqp.CreateChannelAsync(cancellationToken: cancellationToken);

        await canal.ExchangeDeclareAsync(opciones.Exchange, ExchangeType.Topic, durable: true,
            autoDelete: false, cancellationToken: cancellationToken);

        // Cola de muertos: destino final de lo que el consumidor rechaza sin reencolar.
        await canal.QueueDeclareAsync(opciones.ColaMuertos, durable: true, exclusive: false,
            autoDelete: false, cancellationToken: cancellationToken);

        await canal.QueueDeclareAsync(opciones.ColaAvisos, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = opciones.ColaMuertos,
            }, cancellationToken: cancellationToken);
        await canal.QueueBindAsync(opciones.ColaAvisos, opciones.Exchange, opciones.ClaveEnlaceAvisos,
            cancellationToken: cancellationToken);

        foreach (var (sufijo, espera) in opciones.Esperas)
        {
            await canal.QueueDeclareAsync(opciones.ColaEspera(sufijo), durable: true, exclusive: false,
                autoDelete: false, arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = (int)espera.TotalMilliseconds,
                    ["x-dead-letter-exchange"] = "",
                    ["x-dead-letter-routing-key"] = opciones.ColaAvisos,
                }, cancellationToken: cancellationToken);
        }
    }
}
