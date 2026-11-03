namespace UltimaMilla.Infrastructure.Persistence.Configurations;

internal sealed class BultoDevolucionConfiguration : IEntityTypeConfiguration<BultoDevolucion>
{
    public void Configure(EntityTypeBuilder<BultoDevolucion> b)
    {
        Mapping.Entity(b, "BultosDevolucion");
        Mapping.Key(b, "Envio"); Mapping.Key(b, "Devolucion"); Mapping.Key(b, "Bulto");
        b.HasOne(x => x.Devolucion).WithMany(x => x.Bultos).HasForeignKey("DevolucionKey", "EnvioKey")
            .HasPrincipalKey("Id", "EnvioKey");
        b.HasOne(x => x.Bulto).WithMany().HasForeignKey("BultoKey", "EnvioKey").HasPrincipalKey("Id", "EnvioKey");
        b.HasIndex("DevolucionKey", "BultoKey").IsUnique();
    }
}
