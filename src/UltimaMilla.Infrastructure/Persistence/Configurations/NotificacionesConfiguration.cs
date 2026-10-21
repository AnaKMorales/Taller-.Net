namespace UltimaMilla.Infrastructure.Persistence.Configurations;

internal sealed class SuscripcionAvisoConfiguration : IEntityTypeConfiguration<SuscripcionAviso>
{
    public void Configure(EntityTypeBuilder<SuscripcionAviso> b)
    {
        Mapping.Entity(b, "SuscripcionesAviso");
        Mapping.Key(b, "CuentaComercial");
        b.HasOne(x => x.CuentaComercial).WithMany(x => x.Suscripciones).HasForeignKey("CuentaComercialKey");
        Mapping.EnumSet(b.Property(x => x.TiposEvento));
        b.Property(x => x.UrlDestino).HasConversion(value => value.AbsoluteUri, value => new Uri(value));
        Mapping.Collection(b, "Entregas", "_entregas");
    }
}

internal sealed class EntregaAvisoConfiguration : IEntityTypeConfiguration<EntregaAviso>
{
    public void Configure(EntityTypeBuilder<EntregaAviso> b)
    {
        Mapping.Entity(b, "EntregasAviso");
        Mapping.Key(b, "Suscripcion");
        b.HasOne(x => x.Suscripcion).WithMany(x => x.Entregas).HasForeignKey("SuscripcionKey");
        Mapping.Key(b, "Evento");
        b.HasOne(x => x.Evento).WithMany().HasForeignKey("EventoKey");
        b.HasIndex("SuscripcionKey", "EventoKey").IsUnique();
    }
}

internal sealed class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> b)
    {
        Mapping.Entity(b, "Notificaciones");
        Mapping.Key(b, "Destinatario");
        b.HasOne(x => x.Destinatario).WithMany(x => x.Notificaciones).HasForeignKey("DestinatarioKey");
    }
}

