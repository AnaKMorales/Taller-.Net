using System.Linq.Expressions;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

internal static class CuentaOwnership
{
    // Camino hasta la FK de la cuenta. No depende del orden ni de que las navegaciones estén cargadas.
    internal static readonly IReadOnlyDictionary<Type, string[]> Paths = new Dictionary<Type, string[]>
    {
        [typeof(CuentaComercial)] = ["Id"],
        [typeof(Envio)] = ["CuentaComercialKey"],
        [typeof(SuscripcionAviso)] = ["CuentaComercialKey"],
        [typeof(Bulto)] = ["Envio", "CuentaComercialKey"],
        [typeof(Destinatario)] = ["Envio", "CuentaComercialKey"],
        [typeof(Direccion)] = ["Destinatario", "Envio", "CuentaComercialKey"],
        [typeof(EventoDeEnvio)] = ["Envio", "CuentaComercialKey"],
        [typeof(MensajeOutbox)] = ["Evento", "Envio", "CuentaComercialKey"],
        [typeof(IntentoEntrega)] = ["Envio", "CuentaComercialKey"],
        [typeof(PruebaEntrega)] = ["Intento", "Envio", "CuentaComercialKey"],
        [typeof(Parada)] = ["Envio", "CuentaComercialKey"],
        [typeof(Devolucion)] = ["Envio", "CuentaComercialKey"],
        [typeof(Discrepancia)] = ["Envio", "CuentaComercialKey"],
        [typeof(EscaneoBulto)] = ["Bulto", "Envio", "CuentaComercialKey"],
        [typeof(EntregaAviso)] = ["Suscripcion", "CuentaComercialKey"],
        [typeof(Notificacion)] = ["Destinatario", "Envio", "CuentaComercialKey"]
    };

    internal static Expression Owner(Expression row, string[] path)
    {
        Expression current = row;
        foreach (var navigation in path[..^1]) current = Expression.Property(current, navigation);
        return Expression.Call(typeof(EF), nameof(EF.Property), [typeof(Guid)], current, Expression.Constant(path[^1]));
    }

    internal static IQueryable<Guid?> Query<T>(AppDbContext db, Guid id) where T : class
    {
        var row = Expression.Parameter(typeof(T), "row");
        var body = Expression.Convert(Owner(row, Paths[typeof(T)]), typeof(Guid?));
        return db.Set<T>().IgnoreQueryFilters().Where(e => EF.Property<Guid>(e, "Id") == id)
            .Select(Expression.Lambda<Func<T, Guid?>>(body, row));
    }
}
