using Microsoft.EntityFrameworkCore.Metadata;

namespace UltimaMilla.Infrastructure.Tests;

public sealed class ConcurrenciaTests
{
    [Fact]
    public void EnvioYRutaUsanVersionLogicaIndependienteDelTenant()
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        foreach (var type in new[] { typeof(Envio), typeof(Ruta) })
        {
            var version = db.Model.FindEntityType(type)!.FindProperty("RowVersion")!;
            Assert.Equal(typeof(Guid), version.ClrType);
            Assert.True(version.IsShadowProperty());
            Assert.True(version.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.Never, version.ValueGenerated);
            Assert.Equal("gen_random_uuid()", version.GetDefaultValueSql());
        }
        var activeAssignment = db.Model.FindEntityType(typeof(Parada))!.GetIndexes()
            .Single(i => i.IsUnique && i.GetFilter() == "\"Activa\" = TRUE");
        Assert.Equal("IX_Paradas_EnvioId", activeAssignment.GetDatabaseName());
    }
}
