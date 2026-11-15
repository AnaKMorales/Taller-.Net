using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace UltimaMilla.Infrastructure.Persistence.Operacion;

// Modelo de lectura técnico; no modifica estados del dominio ni produce eventos de envío.
public sealed class ResumenOperativoDiario
{
    private ResumenOperativoDiario() { }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateOnly Dia { get; private set; }
    public DateTime ActualizadoEn { get; private set; }
    public DateTime? CerradoEn { get; private set; }
    public int Recalculos { get; private set; }
    public int VersionCalculo { get; private set; } = 1;
    public string ActualJson { get; private set; } = "";
    public string? AlCierreJson { get; private set; }

    internal ResumenOperativoDiario(DateOnly dia) => Dia = dia;
    internal bool Recalcular(string json, DateTime ahora, DateOnly hoy)
    {
        if (ahora.Kind != DateTimeKind.Utc) throw new ArgumentException("El reloj debe expresarse en UTC.");
        ActualJson = json; ActualizadoEn = ahora; Recalculos++;
        if (Dia >= hoy || CerradoEn is not null) return false;
        CerradoEn = ahora; AlCierreJson = json;
        return true;
    }
}

internal sealed class ResumenOperativoDiarioConfiguration : IEntityTypeConfiguration<ResumenOperativoDiario>
{
    public void Configure(EntityTypeBuilder<ResumenOperativoDiario> b)
    {
        b.ToTable("ResumenesOperativosDiarios", t => t.HasCheckConstraint("CK_ResumenOperativo_Cierre",
            "(\"CerradoEn\" IS NULL) = (\"AlCierreJson\" IS NULL)"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property<Guid>("TenantId");
        b.HasIndex("TenantId", nameof(ResumenOperativoDiario.Dia)).IsUnique();
        b.Property(x => x.ActualJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.AlCierreJson).HasColumnType("jsonb");
    }
}
