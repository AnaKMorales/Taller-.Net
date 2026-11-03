namespace UltimaMilla.Infrastructure.Persistence.Configurations;

internal sealed class BultoParadaConfiguration : IEntityTypeConfiguration<BultoParada>
{
    public void Configure(EntityTypeBuilder<BultoParada> b)
    {
        Mapping.Entity(b, "BultosParada");
        Mapping.Key(b, "Envio");
        Mapping.Key(b, "Parada");
        Mapping.Key(b, "Bulto");
        b.HasOne(x => x.Parada).WithMany(x => x.Bultos).HasForeignKey("ParadaKey", "EnvioKey")
            .HasPrincipalKey("Id", "EnvioKey");
        b.HasOne(x => x.Bulto).WithMany().HasForeignKey("BultoKey", "EnvioKey")
            .HasPrincipalKey("Id", "EnvioKey");
        b.HasIndex("ParadaKey", "BultoKey").IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_BultosParada_Medidas", "\"PesoKg\" > 0 AND \"VolumenM3\" > 0"));
    }
}
