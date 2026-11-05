using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Outbox;

// Singleton seguro: solo guarda configuración inmutable; cada llamada crea y libera su propio AppDbContext.
public sealed class PublicacionOutboxPorOperador(DbContextOptions<AppDbContext> opcionesDb, ITransporteMensajes transporte,
    TimeProvider reloj, OpcionesPublicacionOutbox opciones) : IEnumeradorOperadoresOutbox, IPublicadorOutboxPorOperador
{
    public async Task<IReadOnlyList<Guid>> ListarConPendientesAsync(CancellationToken cancellationToken = default)
    {
        // Contexto sin tenant a propósito: los filtros globales no dejarían leer. SQL fijo, sin parámetros
        // y de solo lectura; devuelve únicamente ids de operador. Guardar con este contexto sigue bloqueado.
        await using var db = new AppDbContext(opcionesDb);
        return await db.Database.SqlQuery<Guid>(
                $"""SELECT DISTINCT "TenantId" AS "Value" FROM "MensajesOutbox" WHERE "PublicadoEn" IS NULL""")
            .ToListAsync(cancellationToken);
    }

    public async Task<int> PublicarLoteAsync(Guid operadorId, int maximoMensajes = 100, CancellationToken cancellationToken = default)
    {
        if (operadorId == Guid.Empty) throw new ArgumentException("El operador es obligatorio.", nameof(operadorId));
        await using var db = new AppDbContext(opcionesDb, TenantActual.ParaOperador(operadorId));
        return await new PublicadorOutbox(db, transporte, reloj, opciones)
            .PublicarLoteAsync(maximoMensajes, cancellationToken);
    }
}
