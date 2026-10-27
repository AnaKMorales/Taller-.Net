using System.Data;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Outbox;

// Un contexto dedicado por ejecución/operador. No es un BackgroundService ni un consumidor.
public sealed class PublicadorOutbox(AppDbContext db, ITransporteMensajes transporte,
    TimeProvider reloj, OpcionesPublicacionOutbox opciones) : IPublicadorOutbox
{
    public async Task<int> PublicarLoteAsync(int maximoMensajes = 100, CancellationToken cancellationToken = default)
    {
        if (maximoMensajes is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(maximoMensajes));
        opciones.Validar();
        if (db.ContextoTenant?.Origen != OrigenTenant.ProcesoInterno)
            throw new AislamientoOperadorException();
        if (db.Database.CurrentTransaction is not null || db.ChangeTracker.Entries().Any())
            throw new InvalidOperationException("El publicador requiere un DbContext dedicado, sin entidades cargadas ni transacción previa.");

        var publicados = 0;
        for (var i = 0; i < maximoMensajes; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            MensajeOutbox? pendiente = null;
            try
            {
                // Parámetros interpolados por EF, nunca concatenados. El bloqueo dura hasta commit/rollback.
                // SKIP LOCKED permite que otra instancia tome una fila distinta sin esperar esta publicación.
                pendiente = await db.Set<MensajeOutbox>().FromSqlInterpolated($"""
                    SELECT * FROM "MensajesOutbox"
                    WHERE "TenantId" = {db.OperadorIdActual} AND "PublicadoEn" IS NULL
                    ORDER BY "CreadoEn", "Id"
                    LIMIT 1 FOR UPDATE SKIP LOCKED
                    """).AsTracking().SingleOrDefaultAsync(cancellationToken);
                if (pendiente is null) break;

                var mensaje = new MensajeParaPublicar(pendiente.Id, db.OperadorIdActual,
                    pendiente.Tipo, pendiente.Contenido, pendiente.CreadoEn);
                using var espera = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                espera.CancelAfter(opciones.TiempoMaximoConfirmacion);
                await transporte.PublicarConfirmadoAsync(mensaje, espera.Token);
                espera.Token.ThrowIfCancellationRequested();

                pendiente.MarcarPublicado(reloj.GetUtcNow().UtcDateTime);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                publicados++;
            }
            finally
            {
                // Si hubo error, DisposeAsync revierte la transacción. La fila sigue pendiente.
                // No se reutiliza un estado local marcado como publicado tras un commit fallido.
                if (pendiente is not null) db.Entry(pendiente).State = EntityState.Detached;
            }
        }
        return publicados;
    }
}
