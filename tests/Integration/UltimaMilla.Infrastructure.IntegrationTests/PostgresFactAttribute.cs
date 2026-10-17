namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ULTIMAMILLA_TEST_CONNECTION_STRING")))
            Skip = "Ejecutar scripts/Test-LocalPersistence.ps1 con PostgreSQL local iniciado.";
    }
}
