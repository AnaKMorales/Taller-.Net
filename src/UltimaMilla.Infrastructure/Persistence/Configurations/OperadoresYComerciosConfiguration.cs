namespace UltimaMilla.Infrastructure.Persistence.Configurations;

internal sealed class OperadorConfiguration : IEntityTypeConfiguration<Operador>
{
    public void Configure(EntityTypeBuilder<Operador> b)
    {
        Mapping.Entity(b, "Operadores");
        b.Property(o => o.VersionIdentidad).IsConcurrencyToken().ValueGeneratedNever();
        b.Property(o => o.ColorPrimario).HasDefaultValue("#1E40AF");
        b.Property(o => o.ColorSecundario).HasDefaultValue("#0F172A");
        Mapping.Collection(b, "CuentasComerciales", "_cuentasComerciales");
        Mapping.Collection(b, "Usuarios", "_usuarios");
        Mapping.Collection(b, "Vehiculos", "_vehiculos");
        Mapping.Collection(b, "Zonas", "_zonas");
        Mapping.Collection(b, "VersionesReglas", "_reglas");
        Mapping.Collection(b, "CuadrosTarifarios", "_cuadros");
    }
}

internal sealed class ComercioConfiguration : IEntityTypeConfiguration<Comercio>
{
    public void Configure(EntityTypeBuilder<Comercio> b)
    {
        Mapping.Entity(b, "Comercios");
        Mapping.Collection(b, "CuentasComerciales", "_cuentas");
    }
}

internal sealed class CuentaComercialConfiguration : IEntityTypeConfiguration<CuentaComercial>
{
    public void Configure(EntityTypeBuilder<CuentaComercial> b)
    {
        Mapping.Entity(b, "CuentasComerciales");
        b.Property(c => c.Activa).HasDefaultValue(true);
        b.Property(c => c.Version).IsConcurrencyToken().ValueGeneratedNever();
        Mapping.Key(b, "Operador");
        b.HasOne(x => x.Operador).WithMany(x => x.CuentasComerciales).HasForeignKey("OperadorKey");
        Mapping.Key(b, "Comercio");
        b.HasOne(x => x.Comercio).WithMany(x => x.CuentasComerciales).HasForeignKey("ComercioKey");
        b.HasIndex("OperadorKey", "ComercioKey").IsUnique();
        Mapping.Collection(b, "Envios", "_envios");
        Mapping.Collection(b, "Usuarios", "_usuarios");
        Mapping.Collection(b, "Suscripciones", "_suscripciones");
    }
}

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        Mapping.Entity(b, "Usuarios");
        Mapping.Key(b, "Operador");
        b.HasOne(x => x.Operador).WithMany(x => x.Usuarios).HasForeignKey("OperadorKey");
        b.HasDiscriminator<string>("TipoUsuario").HasValue<Usuario>("Usuario").HasValue<Repartidor>("Repartidor");
        b.Property<string>("EmailNormalizado").HasComputedColumnSql("lower(\"Email\")", stored: true);
        b.HasIndex("OperadorKey", "EmailNormalizado").IsUnique();
        b.HasMany(x => x.CuentasComerciales).WithMany(x => x.Usuarios)
            .UsingEntity<Dictionary<string, object>>("UsuariosCuentasComerciales",
                j => j.HasOne<CuentaComercial>().WithMany().HasForeignKey("CuentaComercialId"),
                j => j.HasOne<Usuario>().WithMany().HasForeignKey("UsuarioId"),
                j => j.HasKey("UsuarioId", "CuentaComercialId"));
        Mapping.Collection(b, "CuentasComerciales", "_cuentas");
    }
}

internal sealed class RepartidorConfiguration : IEntityTypeConfiguration<Repartidor>
{
    public void Configure(EntityTypeBuilder<Repartidor> b)
    {
        b.HasBaseType<Usuario>();
        Mapping.Collection(b, "Rutas", "_rutas");
    }
}

