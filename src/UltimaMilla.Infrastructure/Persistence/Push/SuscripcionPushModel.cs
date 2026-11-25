using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Push;

internal sealed class SuscripcionPushModel : IEntityTypeConfiguration<SuscripcionPush>
{
    public void Configure(EntityTypeBuilder<SuscripcionPush> builder)
    {
        builder.ToTable("SuscripcionesPush");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Endpoint).HasMaxLength(SuscripcionPush.LargoMaximoEndpoint).IsRequired();
        builder.Property(x => x.P256dh).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Auth).HasMaxLength(64).IsRequired();
        // TenantModel agrega la columna y el filtro; se declara acá para poder usarla en el índice.
        builder.Property<Guid>(TenantModel.TenantId);
        builder.HasIndex(nameof(SuscripcionPush.Endpoint), TenantModel.TenantId).IsUnique();
        // TenantModel extiende la FK con el operador: no se puede apuntar a un usuario de otro operador.
        builder.HasOne(x => x.Usuario).WithMany().HasForeignKey("UsuarioId").IsRequired();
    }
}
