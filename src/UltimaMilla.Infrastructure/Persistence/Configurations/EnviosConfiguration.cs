namespace UltimaMilla.Infrastructure.Persistence.Configurations;

internal sealed class EnvioConfiguration : IEntityTypeConfiguration<Envio>
{
    public void Configure(EntityTypeBuilder<Envio> b)
    {
        Mapping.Entity(b, "Envios");
        Mapping.Key(b, "CuentaComercial");
        b.HasOne(x => x.CuentaComercial).WithMany(x => x.Envios).HasForeignKey("CuentaComercialKey");
        Mapping.Key(b, "Destinatario");
        b.HasOne(x => x.Destinatario).WithOne(x => x.Envio).HasForeignKey<Envio>("DestinatarioKey");
        Mapping.Key(b, "Zona");
        b.HasOne(x => x.Zona).WithMany().HasForeignKey("ZonaKey");
        Mapping.Key(b, "Reglas");
        b.HasOne(x => x.Reglas).WithMany().HasForeignKey("ReglasKey");
        Mapping.Key(b, "CuadroTarifario");
        b.HasOne(x => x.CuadroTarifario).WithMany().HasForeignKey("CuadroTarifarioKey");
        Mapping.Key(b, "Programacion");
        b.HasOne(x => x.Programacion).WithMany().HasForeignKey("ProgramacionKey");
        Mapping.Key(b, "Parada", optional: true);
        b.HasOne(x => x.Parada).WithOne().HasForeignKey<Envio>("ParadaKey");
        b.HasIndex(x => x.CodigoSeguimiento).IsUnique();
        Mapping.Collection(b, "Bultos", "_bultos");
        Mapping.Collection(b, "Eventos", "_eventos");
        Mapping.Collection(b, "Intentos", "_intentos");
        Mapping.Collection(b, "Discrepancias", "_discrepancias");
    }
}

internal sealed class BultoConfiguration : IEntityTypeConfiguration<Bulto>
{
    public void Configure(EntityTypeBuilder<Bulto> b)
    {
        Mapping.Entity(b, "Bultos");
        Mapping.Key(b, "Envio");
        b.HasOne(x => x.Envio).WithMany(x => x.Bultos).HasForeignKey("EnvioKey");
        b.Property<string>("CodigoNormalizado").HasComputedColumnSql("lower(\"Codigo\")", stored: true);
        b.HasIndex("EnvioKey", "CodigoNormalizado").IsUnique();
        Mapping.Collection(b, "Escaneos", "_escaneos");
        Mapping.Collection(b, "Discrepancias", "_discrepancias");
    }
}

internal sealed class DestinatarioConfiguration : IEntityTypeConfiguration<Destinatario>
{
    public void Configure(EntityTypeBuilder<Destinatario> b)
    {
        Mapping.Entity(b, "Destinatarios");
        Mapping.Key(b, "Direccion");
        b.HasOne(x => x.Direccion).WithOne(x => x.Destinatario).HasForeignKey<Destinatario>("DireccionKey");
        Mapping.Collection(b, "Notificaciones", "_notificaciones");
    }
}

internal sealed class DireccionConfiguration : IEntityTypeConfiguration<Direccion>
{
    public void Configure(EntityTypeBuilder<Direccion> b)
    {
        Mapping.Entity(b, "Direcciones");
    }
}

internal sealed class EventoDeEnvioConfiguration : IEntityTypeConfiguration<EventoDeEnvio>
{
    public void Configure(EntityTypeBuilder<EventoDeEnvio> b)
    {
        Mapping.Entity(b, "EventosDeEnvio");
        Mapping.Key(b, "Envio");
        b.HasOne(x => x.Envio).WithMany(x => x.Eventos).HasForeignKey("EnvioKey");
        Mapping.Key(b, "Responsable");
        b.HasOne(x => x.Responsable).WithMany().HasForeignKey("ResponsableKey");
        Mapping.Key(b, "ProgramacionAnterior", optional: true);
        b.HasOne(x => x.ProgramacionAnterior).WithMany().HasForeignKey("ProgramacionAnteriorKey");
        Mapping.Key(b, "ProgramacionNueva", optional: true);
        b.HasOne(x => x.ProgramacionNueva).WithMany().HasForeignKey("ProgramacionNuevaKey");
        b.Property(x => x.Posicion).HasConversion(Mapping.GeoConverter!).HasColumnType("jsonb");
        b.HasIndex("EnvioKey", nameof(EventoDeEnvio.IdOperacion)).IsUnique().HasFilter("\"IdOperacion\" IS NOT NULL");
        b.HasIndex("EnvioKey", nameof(EventoDeEnvio.OcurridoEn));
    }
}

internal sealed class IntentoEntregaConfiguration : IEntityTypeConfiguration<IntentoEntrega>
{
    public void Configure(EntityTypeBuilder<IntentoEntrega> b)
    {
        Mapping.Entity(b, "IntentosEntrega");
        Mapping.Key(b, "Envio");
        b.HasOne(x => x.Envio).WithMany(x => x.Intentos).HasForeignKey("EnvioKey");
        Mapping.Key(b, "Parada");
        b.HasOne(x => x.Parada).WithMany(x => x.Intentos).HasForeignKey("ParadaKey");
        Mapping.Key(b, "MotivoNoEntrega", optional: true);
        b.HasOne(x => x.MotivoNoEntrega).WithMany().HasForeignKey("MotivoNoEntregaKey");
        b.HasIndex("EnvioKey", nameof(IntentoEntrega.IdOperacion)).IsUnique();
        b.HasIndex("EnvioKey", nameof(IntentoEntrega.Numero)).IsUnique();
        Mapping.Collection(b, "Pruebas", "_pruebas");
    }
}

internal sealed class PruebaEntregaConfiguration : IEntityTypeConfiguration<PruebaEntrega>
{
    public void Configure(EntityTypeBuilder<PruebaEntrega> b)
    {
        Mapping.Entity(b, "PruebasEntrega");
        Mapping.Key(b, "Intento");
        b.HasOne(x => x.Intento).WithMany(x => x.Pruebas).HasForeignKey("IntentoKey");
        b.Property(x => x.Posicion).HasConversion(Mapping.GeoConverter!).HasColumnType("jsonb");
        b.Property(x => x.ArchivoRef).HasConversion(Mapping.ArchivoConverter!);
    }
}

internal sealed class ProgramacionEntregaConfiguration : IEntityTypeConfiguration<ProgramacionEntrega>
{
    public void Configure(EntityTypeBuilder<ProgramacionEntrega> b)
    {
        b.ToTable("ProgramacionesEntrega");
        b.Property<Guid>("Id").ValueGeneratedOnAdd();
        b.HasKey("Id");
        Mapping.Key(b, "Franja");
        b.HasOne(x => x.Franja).WithMany().HasForeignKey("FranjaKey");
        b.Ignore(x => x.FranjaId);
        b.Ignore(x => x.VentanaDesde);
        b.Ignore(x => x.VentanaHasta);
        b.Property(x => x.InicioLocal).HasConversion(Mapping.LocalDateConverter).HasMaxLength(40);
        b.Property(x => x.FinLocal).HasConversion(Mapping.LocalDateConverter).HasMaxLength(40);
    }
}

