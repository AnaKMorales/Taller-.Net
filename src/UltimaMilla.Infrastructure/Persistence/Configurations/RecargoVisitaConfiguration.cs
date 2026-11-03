namespace UltimaMilla.Infrastructure.Persistence.Configurations;

internal sealed class RecargoVisitaConfiguration : IEntityTypeConfiguration<RecargoVisita>
{
    public void Configure(EntityTypeBuilder<RecargoVisita> b)
    {
        Mapping.Entity(b, "RecargosVisita");
        Mapping.Key(b, "Envio"); Mapping.Key(b, "Parada");
        b.HasOne(x => x.Envio).WithMany(x => x.Recargos).HasForeignKey("EnvioKey");
        b.HasOne(x => x.Parada).WithOne(x => x.Recargo).HasForeignKey<RecargoVisita>("ParadaKey", "EnvioKey")
            .HasPrincipalKey<Parada>("Id", "EnvioKey");
        b.HasIndex("ParadaKey").IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_RecargosVisita_Importe", "\"Importe\" > 0 AND round(\"Importe\", 2) = \"Importe\""));
    }
}
