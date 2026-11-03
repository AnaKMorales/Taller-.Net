using Microsoft.EntityFrameworkCore.Diagnostics;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Infrastructure.Persistence.Concurrency;
using UltimaMilla.Infrastructure.Persistence.Outbox;
using UltimaMilla.Infrastructure.Persistence.Configuracion;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

// Sin estado de petición: un singleton puede validar contextos distintos sin compartir su tenant.
internal sealed class TenantSaveChangesInterceptor : SaveChangesInterceptor
{
    internal static readonly TenantSaveChangesInterceptor Instance = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is AppDbContext db) Validate(db, useAsync: false).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AppDbContext db) await Validate(db, useAsync: true, cancellationToken);
        return result;
    }

    private static async Task Validate(AppDbContext db, bool useAsync, CancellationToken token = default)
    {
        if (db.EsSeguimientoPublico) throw new AislamientoOperadorException();
        OutboxPreparation.Prepare(db);
        await TenantWriteGuard.ValidateAsync(db, db.OperadorIdActual, useAsync, token);
        await CuentaWriteGuard.ValidateAsync(db, db.ContextoTenant!, useAsync, token);
        ConfiguracionWriteGuard.Validate(db);
        IntentosBultoWriteGuard.Validate(db);
        AsignacionesBultoWriteGuard.Validate(db);
        RecargosVisitaWriteGuard.Validate(db);
        if (AggregateConcurrency.Prepare(db))
        {
            // Las raíces antes intactas pasan a UPDATE; también deben validar ambos ámbitos.
            await TenantWriteGuard.ValidateAsync(db, db.OperadorIdActual, useAsync, token);
            await CuentaWriteGuard.ValidateAsync(db, db.ContextoTenant!, useAsync, token);
        }
    }
}
