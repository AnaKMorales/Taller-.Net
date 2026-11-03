namespace UltimaMilla.Infrastructure.Persistence.Configurations;

internal sealed class ZonaCoberturaConfiguration : IEntityTypeConfiguration<ZonaCobertura>
{
    public void Configure(EntityTypeBuilder<ZonaCobertura> b)
    {
        Mapping.Entity(b, "ZonasCobertura");
        Mapping.Key(b, "Operador");
        b.HasOne(x => x.Operador).WithMany(x => x.Zonas).HasForeignKey("OperadorKey");
        Mapping.Collection(b, "Franjas", "_franjas");
    }
}

internal sealed class FranjaHorariaConfiguration : IEntityTypeConfiguration<FranjaHoraria>
{
    public void Configure(EntityTypeBuilder<FranjaHoraria> b)
    {
        Mapping.Entity(b, "FranjasHorarias");
        Mapping.Key(b, "Zona");
        b.HasOne(x => x.Zona).WithMany(x => x.Franjas).HasForeignKey("ZonaKey");
    }
}

internal sealed class ReglasOperativasConfiguration : IEntityTypeConfiguration<ReglasOperativas>
{
    public void Configure(EntityTypeBuilder<ReglasOperativas> b)
    {
        Mapping.Entity(b, "ReglasOperativas");
        b.Property(x => x.PermiteEntregaParcial).HasDefaultValue(false);
        b.Property(x => x.AccionPendientes).HasDefaultValue(AccionBultosPendientes.NuevaVisitaSinRecargo);
        b.ToTable(t => t.HasCheckConstraint("CK_ReglasOperativas_AccionPendientes", "\"AccionPendientes\" IN (0, 1, 2)"));
        Mapping.Key(b, "Operador");
        b.HasOne(x => x.Operador).WithMany(x => x.VersionesReglas).HasForeignKey("OperadorKey");
        Mapping.Key(b, "PoliticaDevolucion");
        b.HasOne(x => x.PoliticaDevolucion).WithOne(x => x.Reglas).HasForeignKey<ReglasOperativas>("PoliticaDevolucionKey");
        b.HasIndex("OperadorKey", nameof(ReglasOperativas.Version)).IsUnique();
        Mapping.EnumSet(b.Property(x => x.TiposPrueba));
        Mapping.Collection(b, "MotivosNoEntrega", "_motivos");
        Mapping.Collection(b, "PlazosServicio", "_plazos");
    }
}

internal sealed class PoliticaDevolucionConfiguration : IEntityTypeConfiguration<PoliticaDevolucion>
{
    public void Configure(EntityTypeBuilder<PoliticaDevolucion> b)
    {
        Mapping.Entity(b, "PoliticasDevolucion");
    }
}

internal sealed class MotivoNoEntregaConfiguration : IEntityTypeConfiguration<MotivoNoEntrega>
{
    public void Configure(EntityTypeBuilder<MotivoNoEntrega> b)
    {
        Mapping.Entity(b, "MotivosNoEntrega");
        Mapping.Key(b, "Reglas");
        b.HasOne(x => x.Reglas).WithMany(x => x.MotivosNoEntrega).HasForeignKey("ReglasKey");
    }
}

internal sealed class PlazoServicioConfiguration : IEntityTypeConfiguration<PlazoServicio>
{
    public void Configure(EntityTypeBuilder<PlazoServicio> b)
    {
        Mapping.Entity(b, "PlazosServicio");
        Mapping.Key(b, "Reglas");
        b.HasOne(x => x.Reglas).WithMany(x => x.PlazosServicio).HasForeignKey("ReglasKey");
        b.HasIndex("ReglasKey", nameof(PlazoServicio.ModalidadServicio)).IsUnique();
    }
}

internal sealed class CuadroTarifarioConfiguration : IEntityTypeConfiguration<CuadroTarifario>
{
    public void Configure(EntityTypeBuilder<CuadroTarifario> b)
    {
        Mapping.Entity(b, "CuadrosTarifarios");
        Mapping.Key(b, "Operador");
        b.HasOne(x => x.Operador).WithMany(x => x.CuadrosTarifarios).HasForeignKey("OperadorKey");
        b.HasIndex("OperadorKey", nameof(CuadroTarifario.Version)).IsUnique();
        Mapping.Collection(b, "Lineas", "_lineas");
    }
}

internal sealed class LineaTarifaConfiguration : IEntityTypeConfiguration<LineaTarifa>
{
    public void Configure(EntityTypeBuilder<LineaTarifa> b)
    {
        Mapping.Entity(b, "LineasTarifa");
        Mapping.Key(b, "Cuadro");
        b.HasOne(x => x.Cuadro).WithMany(x => x.Lineas).HasForeignKey("CuadroKey");
        Mapping.Key(b, "Zona");
        b.HasOne(x => x.Zona).WithMany().HasForeignKey("ZonaKey");
        Mapping.Collection(b, "Ajustes", "_ajustes");
    }
}

internal sealed class AjusteTarifarioConfiguration : IEntityTypeConfiguration<AjusteTarifario>
{
    public void Configure(EntityTypeBuilder<AjusteTarifario> b)
    {
        Mapping.Entity(b, "AjustesTarifarios");
        Mapping.Key(b, "Linea");
        b.HasOne(x => x.Linea).WithMany(x => x.Ajustes).HasForeignKey("LineaKey");
    }
}

