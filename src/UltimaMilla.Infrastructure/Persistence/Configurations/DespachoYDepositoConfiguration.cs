namespace UltimaMilla.Infrastructure.Persistence.Configurations;

internal sealed class VehiculoConfiguration : IEntityTypeConfiguration<Vehiculo>
{
    public void Configure(EntityTypeBuilder<Vehiculo> b)
    {
        Mapping.Entity(b, "Vehiculos");
        Mapping.Key(b, "Operador");
        b.HasOne(x => x.Operador).WithMany(x => x.Vehiculos).HasForeignKey("OperadorKey");
        b.Property<string>("MatriculaNormalizada").HasComputedColumnSql("lower(\"Matricula\")", stored: true);
        b.HasIndex("OperadorKey", "MatriculaNormalizada").IsUnique();
        Mapping.Collection(b, "Rutas", "_rutas");
    }
}

internal sealed class RutaConfiguration : IEntityTypeConfiguration<Ruta>
{
    public void Configure(EntityTypeBuilder<Ruta> b)
    {
        Mapping.Entity(b, "Rutas");
        Mapping.Key(b, "Repartidor");
        b.HasOne(x => x.Repartidor).WithMany(x => x.Rutas).HasForeignKey("RepartidorKey");
        Mapping.Key(b, "Vehiculo");
        b.HasOne(x => x.Vehiculo).WithMany(x => x.Rutas).HasForeignKey("VehiculoKey");
        Mapping.Collection(b, "Paradas", "_paradas");
        Mapping.Collection(b, "Escaneos", "_escaneos");
    }
}

internal sealed class ParadaConfiguration : IEntityTypeConfiguration<Parada>
{
    public void Configure(EntityTypeBuilder<Parada> b)
    {
        Mapping.Entity(b, "Paradas");
        Mapping.Key(b, "Ruta");
        b.HasOne(x => x.Ruta).WithMany(x => x.Paradas).HasForeignKey("RutaKey");
        Mapping.Key(b, "Envio");
        b.HasOne(x => x.Envio).WithMany().HasForeignKey("EnvioKey");
        b.HasIndex("RutaKey", nameof(Parada.Cod)).IsUnique();
        b.HasIndex("EnvioKey").IsUnique().HasFilter("\"Activa\" = TRUE");
        Mapping.Collection(b, "Intentos", "_intentos");
        b.ToTable(t => t.HasCheckConstraint("CK_Paradas_RecargoPrevisto", "\"RecargoPrevisto\" >= 0 AND round(\"RecargoPrevisto\", 2) = \"RecargoPrevisto\""));
        Mapping.Collection(b, "Bultos", "_bultos");
    }
}

internal sealed class EscaneoBultoConfiguration : IEntityTypeConfiguration<EscaneoBulto>
{
    public void Configure(EntityTypeBuilder<EscaneoBulto> b)
    {
        Mapping.Entity(b, "EscaneosBulto");
        Mapping.Key(b, "Ruta", optional: true);
        b.HasOne(x => x.Ruta).WithMany(x => x.Escaneos).HasForeignKey("RutaKey");
        Mapping.Key(b, "Devolucion", optional: true);
        b.HasOne(x => x.Devolucion).WithMany().HasForeignKey("DevolucionKey");
        Mapping.Key(b, "Bulto");
        b.HasOne(x => x.Bulto).WithMany(x => x.Escaneos).HasForeignKey("BultoKey");
        Mapping.Key(b, "Responsable");
        b.HasOne(x => x.Responsable).WithMany().HasForeignKey("ResponsableKey");
    }
}

internal sealed class DiscrepanciaConfiguration : IEntityTypeConfiguration<Discrepancia>
{
    public void Configure(EntityTypeBuilder<Discrepancia> b)
    {
        Mapping.Entity(b, "Discrepancias");
        Mapping.Key(b, "Envio");
        b.HasOne(x => x.Envio).WithMany(x => x.Discrepancias).HasForeignKey("EnvioKey");
        Mapping.Key(b, "Bulto", optional: true);
        b.HasOne(x => x.Bulto).WithMany(x => x.Discrepancias).HasForeignKey("BultoKey");
    }
}

internal sealed class DevolucionConfiguration : IEntityTypeConfiguration<Devolucion>
{
    public void Configure(EntityTypeBuilder<Devolucion> b)
    {
        Mapping.Entity(b, "Devoluciones");
        Mapping.Collection(b, "Bultos", "_bultos");
        Mapping.Key(b, "Envio");
        b.HasOne(x => x.Envio).WithOne(x => x.Devolucion).HasForeignKey<Devolucion>("EnvioKey");
    }
}

internal sealed class RendicionConfiguration : IEntityTypeConfiguration<Rendicion>
{
    public void Configure(EntityTypeBuilder<Rendicion> b)
    {
        Mapping.Entity(b, "Rendiciones");
        Mapping.Key(b, "Ruta");
        b.HasOne(x => x.Ruta).WithOne(x => x.Rendicion).HasForeignKey<Rendicion>("RutaKey");
    }
}


internal sealed class PosicionVehiculoConfiguration : IEntityTypeConfiguration<PosicionVehiculo>
{
    public void Configure(EntityTypeBuilder<PosicionVehiculo> b)
    {
        Mapping.Entity(b, "PosicionesVehiculo");
        Mapping.Key(b, "Ruta");
        // Sin colección en Ruta: una lectura de GPS no forma parte del agregado ni cambia su versión.
        b.HasOne(x => x.Ruta).WithMany().HasForeignKey("RutaKey");
        b.Property(x => x.Punto).HasConversion(Mapping.GeoConverter!).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.PrecisionMetros).HasPrecision(8, 2);
        // Reenviar el mismo lote desde la cola offline no duplica filas.
        b.HasIndex("RutaKey", nameof(PosicionVehiculo.IdLectura)).IsUnique();
        // El mapa de la flota busca la última lectura reciente de cada repartidor.
        b.HasIndex(nameof(PosicionVehiculo.RegistradaEn));
    }
}
