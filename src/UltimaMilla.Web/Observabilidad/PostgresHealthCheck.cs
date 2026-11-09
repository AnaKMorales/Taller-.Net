using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace UltimaMilla.Web.Observabilidad;

public sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("UltimaMilla");
        if (string.IsNullOrWhiteSpace(connectionString))
            return HealthCheckResult.Unhealthy("PostgreSQL no está configurado.");

        // Una consulta técnica, sin resolver tenant ni leer datos del negocio.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
