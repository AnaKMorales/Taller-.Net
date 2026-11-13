using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UltimaMilla.Domain.Comercios;

namespace UltimaMilla.Infrastructure.Persistence.Envios;

// Registro técnico permanente: no contiene el archivo ni los datos del destinatario.
internal sealed class AltaImportada
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public CuentaComercial Cuenta { get; private set; } = null!;
    public Envio Envio { get; private set; } = null!;
    public string Referencia { get; private set; } = null!;
    public string Huella { get; private set; } = null!;
    public string Resultado { get; private set; } = null!;
    private AltaImportada() { }
    internal AltaImportada(CuentaComercial cuenta, Envio envio, string referencia, string huella, string resultado)
    { Cuenta = cuenta; Envio = envio; Referencia = referencia; Huella = huella; Resultado = resultado; }
}

internal sealed class AltaImportadaConfiguration : IEntityTypeConfiguration<AltaImportada>
{
    public void Configure(EntityTypeBuilder<AltaImportada> b)
    {
        b.ToTable("AltasImportadas");
        b.HasKey(x => x.Id);
        b.Property<Guid>("TenantId");
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Referencia).HasMaxLength(100).IsRequired();
        b.Property(x => x.Huella).HasMaxLength(64).IsRequired();
        b.Property(x => x.Resultado).IsRequired();
        b.HasOne(x => x.Cuenta).WithMany().HasForeignKey("CuentaComercialKey");
        b.HasOne(x => x.Envio).WithMany().HasForeignKey("EnvioKey");
        b.HasIndex("TenantId", "CuentaComercialKey", nameof(AltaImportada.Referencia)).IsUnique();
    }
}
