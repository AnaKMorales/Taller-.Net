using Microsoft.EntityFrameworkCore;
using Npgsql;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Inbox;

// Singleton seguro: solo guarda configuración; cada llamada crea y libera su propio AppDbContext.
public sealed class RegistroIdempotencia(DbContextOptions<AppDbContext> opciones, TimeProvider reloj) : IRegistroIdempotencia
{
    public async Task<bool> EjecutarUnaVezAsync(TenantContext tenant, Guid mensajeId, string consumidor,
        Func<CancellationToken, Task> accion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(accion);
        // Registro técnico del operador: se escribe con contexto de nivel operador, nunca restringido a una
        // cuenta (CuentaWriteGuard rechaza escrituras ajenas a la cuenta). El operador viene del tenant ya verificado.
        await using var db = new AppDbContext(opciones, TenantActual.ParaOperador(tenant.OperadorId));
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Add(new MensajeProcesado(mensajeId, consumidor, reloj.GetUtcNow().UtcDateTime));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Otro proceso ya lo reservó y confirmó (si seguía en curso, el INSERT esperó a su resultado).
            return false;
        }
        // La reserva y la acción comparten transacción: si la acción falla, no queda marcado como procesado.
        await accion(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return true;
    }
}
