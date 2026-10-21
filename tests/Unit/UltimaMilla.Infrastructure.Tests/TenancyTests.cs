using UltimaMilla.Application.Tenancy;
using Microsoft.EntityFrameworkCore.Metadata;
using UltimaMilla.Application.Abstractions;

namespace UltimaMilla.Infrastructure.Tests;

public sealed class TenancyTests
{
    [Fact]
    public void TodasLasRaicesTienenFiltroYLasRelacionesPrivadasIncluyenTenant()
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        var roots = db.Model.GetEntityTypes().Where(t => t.BaseType is null).ToArray();
        Assert.All(roots, type =>
        {
            Assert.NotNull(type.FindDeclaredQueryFilter("AislamientoOperador"));
            Assert.NotNull(type.FindDeclaredQueryFilter("AislamientoCuenta"));
        });
        Assert.All(roots.Where(t => t.ClrType != typeof(Operador) && t.ClrType != typeof(Comercio)), type =>
        {
            var tenant = type.FindProperty("TenantId")!;
            Assert.False(tenant.IsNullable);
            Assert.True(tenant.IsConcurrencyToken);
            Assert.Contains(type.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(Operador)
                && fk.Properties.SingleOrDefault()?.Name == "TenantId");
        });
        Assert.All(db.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys())
            .Where(f => f.PrincipalEntityType.FindProperty("TenantId") is not null),
            fk => Assert.Contains(fk.Properties, p => p.Name == "TenantId"));
    }

    [Fact]
    public void OperadorVacioSeRechaza()
    {
        Assert.Throws<ArgumentException>(() => TenantActual.ParaOperador(Guid.Empty));
    }

    [Fact]
    public void SinOperadorSeRechazaSaveChangesSincrono()
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        db.Add(new Operador("Sin ámbito"));
        Assert.Throws<AislamientoOperadorException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task SinOperadorSeRechazaSaveChangesAsincrono()
    {
        await using var db = new AppDbContextFactory().CreateDbContext([]);
        db.Add(new Operador("Sin ámbito"));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public void GrafoDeOtroOperadorSeRechazaAntesDeConectar()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=no_conectar").Options;
        using var db = new AppDbContext(options, TenantActual.ParaOperador(Guid.NewGuid()));
        db.Add(new Operador("Otro operador"));
        Assert.Throws<AislamientoOperadorException>(() => db.SaveChanges());
    }
}
