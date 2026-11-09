using Npgsql;
using UltimaMilla.Infrastructure.Mensajeria;

namespace UltimaMilla.Infrastructure.Observabilidad;

public sealed class SondeoMetricasOperativas(string connectionString, ConexionRabbitMq rabbit, OpcionesRabbitMq opciones)
{
    public async Task MedirBaseAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        // Agregados técnicos globales del proceso interno. No se exportan operadores ni datos personales.
        await using var command = new NpgsqlCommand("""
            SELECT (SELECT count(*) FROM "MensajesOutbox" WHERE "PublicadoEn" IS NULL),
                   (SELECT count(*) FROM "EntregasAviso" WHERE NOT "Exitoso" AND "Intentos" > 0)
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        MetricasOperativas.EstadoBase(reader.GetInt64(0), reader.GetInt64(1));
    }

    public async Task MedirColasAsync(CancellationToken ct)
    {
        var connection = await rabbit.ObtenerAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        var muestras = new List<(string Cola, long Listos)>();
        foreach (var cola in new[] { opciones.ColaAvisos, opciones.ColaMuertos }
            .Concat(opciones.Esperas.Select(e => opciones.ColaEspera(e.Sufijo))))
        {
            var estado = await channel.QueueDeclarePassiveAsync(cola, ct);
            muestras.Add((cola, estado.MessageCount));
        }
        foreach (var muestra in muestras) MetricasOperativas.EstadoCola(muestra.Cola, muestra.Listos);
        MetricasOperativas.ColasActualizadas();
    }
}
