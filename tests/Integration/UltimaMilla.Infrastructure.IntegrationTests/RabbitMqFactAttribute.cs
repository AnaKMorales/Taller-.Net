namespace UltimaMilla.Infrastructure.IntegrationTests;

// Se omite si no hay clave configurada. Variables: ULTIMAMILLA_TEST_RABBITMQ_CLAVE (obligatoria),
// y opcionales _HOST (127.0.0.1), _PUERTO (55672), _USUARIO (ultimamilla).
public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Variable("CLAVE")))
            Skip = "Definir ULTIMAMILLA_TEST_RABBITMQ_CLAVE con RabbitMQ del compose iniciado (deploy/compose.yaml).";
    }

    internal static string? Variable(string nombre) =>
        Environment.GetEnvironmentVariable("ULTIMAMILLA_TEST_RABBITMQ_" + nombre);
}
