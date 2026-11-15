using Microsoft.AspNetCore.Identity;

namespace UltimaMilla.Infrastructure.Persistence.Identidad;

internal static class IdentidadModel
{
    // Tablas de Identity: sin TenantId ni filtro de operador, porque el login busca al usuario antes de conocer su
    // operador. Las escrituras las valida IdentidadWriteGuard a través del Usuario dueño.
    internal static readonly HashSet<Type> Tipos =
    [
        typeof(CuentaAcceso), typeof(IdentityUserClaim<Guid>), typeof(IdentityUserLogin<Guid>), typeof(IdentityUserToken<Guid>),
        typeof(IdentityUserPasskey<Guid>), typeof(IdentityPasskeyData)
    ];

    internal static bool EsIdentidad(Type tipo) => Tipos.Contains(tipo);
}

internal sealed class CuentaAccesoConfiguration : IEntityTypeConfiguration<CuentaAcceso>
{
    public void Configure(EntityTypeBuilder<CuentaAcceso> builder) =>
        // Misma clave que el Usuario del dominio: una cuenta de acceso por usuario.
        builder.HasOne<Usuario>().WithOne().HasForeignKey<CuentaAcceso>(c => c.Id).OnDelete(DeleteBehavior.Restrict);
}
