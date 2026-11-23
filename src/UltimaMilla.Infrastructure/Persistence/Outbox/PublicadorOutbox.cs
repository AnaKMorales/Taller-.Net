using System.Data;
using UltimaMilla.Infrastructure.Observabilidad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Outbox;

// Un contexto dedicado por ejecución/operador. No es un BackgroundService ni un consumidor.
public sealed class PublicadorOutbox(AppDbContext db, ITransporteMensajes transporte,
    TimeProvider reloj, OpcionesPublicacionOutbox opciones, ILogger<PublicadorOutbox>? logger = null) : IPublicadorOutbox
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
                    WHERE "TenantId" = {db.OperadorIdActual} AND "PublicadoEn" IS NULL AND "CuarentenaEn" IS NULL
                    ORDER BY "CreadoEn", "Id"
                    LIMIT 1 FOR UPDATE SKIP LOCKED
                    """).AsTracking().SingleOrDefaultAsync(cancellationToken);
                if (pendiente is null) break;

                var mensaje = new MensajeParaPublicar(pendiente.Id, db.OperadorIdActual,
                    pendiente.Tipo, pendiente.Contenido, pendiente.CreadoEn, pendiente.TraceParent, pendiente.TraceState);
                using var espera = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                espera.CancelAfter(opciones.TiempoMaximoConfirmacion);
                await transporte.PublicarConfirmadoAsync(mensaje, espera.Token);
                espera.Token.ThrowIfCancellationRequested();

                pendiente.MarcarPublicado(reloj.GetUtcNow().UtcDateTime);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                publicados++;
                MetricasOperativas.PublicacionConfirmada();
            }
            catch (MensajeNoPublicableException error) when (pendiente is not null && !cancellationToken.IsCancellationRequested)
            {
                MetricasOperativas.PublicacionFallida();
                pendiente.RegistrarFallo(reloj.GetUtcNow().UtcDateTime, opciones.MaximoIntentosPorMensaje);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                if (pendiente.CuarentenaEn is null) throw;

                logger?.LogError(error,
                    "Mensaje {MensajeId} del operador {OperadorId} en cuarentena tras {Intentos} fallos de publicación.",
                    pendiente.Id, db.OperadorIdActual, pendiente.IntentosFallidos);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                MetricasOperativas.PublicacionFallida();
                throw;
            }
            finally
            {
                // Se descarta el estado local también si DisposeAsync debe revertir un guardado fallido.
                if (pendiente is not null) db.Entry(pendiente).State = EntityState.Detached;
            }
        }
        return publicados;
    }
}
