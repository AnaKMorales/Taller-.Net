using System.Linq.Expressions;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

internal static class CuentaModel
{
    internal const string FilterName = "AislamientoCuenta";
    private static readonly HashSet<Type> Catalogs = [typeof(Operador), typeof(ZonaCobertura), typeof(FranjaHoraria),
        typeof(ReglasOperativas), typeof(PoliticaDevolucion), typeof(MotivoNoEntrega), typeof(PlazoServicio),
        typeof(CuadroTarifario), typeof(LineaTarifa), typeof(AjusteTarifario)];

    internal static void Configure(ModelBuilder builder, AppDbContext context)
    {
        foreach (var type in builder.Model.GetEntityTypes().Where(t => t.BaseType is null).ToArray())
        {
            if (type.ClrType == typeof(ProgramacionEntrega) || type.ClrType == typeof(Comercio) ||
                type.ClrType == typeof(Usuario) || type.ClrType == typeof(Dictionary<string, object>)) continue;
            var row = Expression.Parameter(type.ClrType, "row");
            Expression<Func<bool>> unrestricted = () => !context.CuentaRestringida;
            Expression<Func<bool>> notPublic = () => !context.EsSeguimientoPublico;
            Expression predicate;
            if (CuentaOwnership.Paths.TryGetValue(type.ClrType, out var path))
            {
                Expression<Func<Guid?>> account = () => context.CuentaComercialIdActual;
                var matches = Expression.Equal(Expression.Convert(CuentaOwnership.Owner(row, path), typeof(Guid?)), account.Body);
                predicate = Expression.OrElse(unrestricted.Body, matches);
            }
            else predicate = Catalogs.Contains(type.ClrType) ? Expression.Constant(true) : unrestricted.Body;

            if (type.ClrType == typeof(Envio))
            {
                Expression<Func<Guid?>> publicId = () => context.EnvioPublicoId;
                predicate = Expression.AndAlso(predicate, Expression.OrElse(notPublic.Body,
                    Expression.Equal(Expression.Convert(Expression.Property(row, "Id"), typeof(Guid?)), publicId.Body)));
            }
            else if (type.ClrType == typeof(EventoDeEnvio))
            {
                Expression<Func<Guid?>> publicId = () => context.EnvioPublicoId;
                predicate = Expression.AndAlso(predicate, Expression.OrElse(notPublic.Body,
                    Expression.Equal(Expression.Convert(Expression.Call(typeof(EF), nameof(EF.Property), [typeof(Guid)],
                        row, Expression.Constant("EnvioKey")), typeof(Guid?)), publicId.Body)));
            }
            else predicate = Expression.AndAlso(predicate, notPublic.Body);
            builder.Entity(type.Name).HasQueryFilter(FilterName, Expression.Lambda(predicate, row));
        }

        builder.Entity<Comercio>().HasQueryFilter(FilterName, row => !context.EsSeguimientoPublico &&
            (!context.CuentaRestringida || row.CuentasComerciales.Any(a => a.Id == context.CuentaComercialIdActual)));
        builder.Entity<Usuario>().HasQueryFilter(FilterName, row => !context.EsSeguimientoPublico &&
            (!context.CuentaRestringida || row.Id == context.UsuarioIdActual));
        builder.SharedTypeEntity<Dictionary<string, object>>("UsuariosCuentasComerciales").HasQueryFilter(FilterName,
            row => !context.EsSeguimientoPublico && (!context.CuentaRestringida ||
                (EF.Property<Guid>(row, "CuentaComercialId") == context.CuentaComercialIdActual &&
                 EF.Property<Guid>(row, "UsuarioId") == context.UsuarioIdActual)));
        builder.Entity<ProgramacionEntrega>().HasQueryFilter(FilterName, row => !context.CuentaRestringida ||
            context.Envios.Any(e => EF.Property<Guid>(e, "ProgramacionKey") == EF.Property<Guid>(row, "Id") &&
                EF.Property<Guid>(e, "CuentaComercialKey") == context.CuentaComercialIdActual) ||
            context.Set<EventoDeEnvio>().Any(e =>
                (EF.Property<Guid?>(e, "ProgramacionAnteriorKey") == EF.Property<Guid>(row, "Id") ||
                 EF.Property<Guid?>(e, "ProgramacionNuevaKey") == EF.Property<Guid>(row, "Id")) &&
                EF.Property<Guid>(e.Envio, "CuentaComercialKey") == context.CuentaComercialIdActual));
    }

}
