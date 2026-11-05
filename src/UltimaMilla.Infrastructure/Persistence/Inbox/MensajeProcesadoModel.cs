using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UltimaMilla.Infrastructure.Persistence.Inbox;

internal sealed class MensajeProcesadoModel : IEntityTypeConfiguration<MensajeProcesado>
{
    public void Configure(EntityTypeBuilder<MensajeProcesado> builder)
    {
        builder.ToTable("MensajesProcesados");
        builder.HasKey(x => new { x.MensajeId, x.Consumidor });
        builder.Property(x => x.MensajeId).ValueGeneratedNever();
        builder.Property(x => x.Consumidor).HasMaxLength(100).IsRequired();
    }
}
