using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UltimaMilla.Infrastructure.Persistence.Outbox;

internal sealed class OutboxModel : IEntityTypeConfiguration<MensajeOutbox>
{
    public void Configure(EntityTypeBuilder<MensajeOutbox> builder)
    {
        builder.ToTable("MensajesOutbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property<Guid>("EventoKey").HasColumnName("EventoId");
        builder.HasOne(x => x.Evento).WithMany().HasForeignKey("EventoKey");
        builder.HasIndex("EventoKey").IsUnique();
        builder.Property(x => x.Tipo).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Contenido).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.TraceParent).HasMaxLength(55);
        builder.Property(x => x.TraceState).HasMaxLength(512);
        builder.HasIndex(x => new { x.CreadoEn, x.Id }).HasFilter("\"PublicadoEn\" IS NULL");
    }
}
