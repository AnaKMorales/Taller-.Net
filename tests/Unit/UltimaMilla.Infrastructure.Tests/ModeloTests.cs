using Microsoft.EntityFrameworkCore.Metadata;

namespace UltimaMilla.Infrastructure.Tests;

// Estas pruebas construyen el modelo Npgsql y generan SQL; no ejecutan SQL en PostgreSQL.
public sealed class ModeloTests
{
    private static AppDbContext Context() => new AppDbContextFactory().CreateDbContext([]);

    [Fact]
    public void MapeaTodasLasEntidadesSinForeignKeysAccidentales()
    {
        using var db = Context();
        var domainTypes = typeof(Entidad).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(Entidad).IsAssignableFrom(t)).ToArray();
        Assert.Equal(37, domainTypes.Length);
        Assert.All(domainTypes, t => Assert.NotNull(db.Model.FindEntityType(t)));
        // Incluye la tabla de credenciales WebAuthn de Identity y la de suscripciones Web Push del repartidor.
        Assert.Equal(48, db.Model.GetEntityTypes().Select(t => t.GetTableName()).Distinct().Count());
        var resumen = db.Model.FindEntityType(typeof(UltimaMilla.Infrastructure.Persistence.Operacion.ResumenOperativoDiario))!;
        Assert.Equal("ResumenesOperativosDiarios", resumen.GetTableName());
        Assert.Contains(resumen.GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Dia" }));
        Assert.DoesNotContain(db.Model.GetEntityTypes(), t => t.ClrType == typeof(GeoPunto) || t.ClrType == typeof(ArchivoRef));
        Assert.All(db.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()), fk =>
        {
            Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
            // Las FK de Identity son propiedades propias de sus clases (UserId, Id), no shadow.
            if (fk.DeclaringEntityType.ClrType.Namespace != "Microsoft.AspNetCore.Identity" &&
                fk.DeclaringEntityType.ClrType != typeof(UltimaMilla.Infrastructure.Persistence.Identidad.CuentaAcceso))
                Assert.All(fk.Properties, p => Assert.True(p.IsShadowProperty() || p.IsIndexerProperty()));
            Assert.DoesNotContain(fk.Properties, p => p.Name.EndsWith("Key1", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void GeneraEsquemaPostgreSqlConTiposYRestricciones()
    {
        using var db = Context();
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("CREATE TABLE \"Envios\"", sql);
        Assert.Contains("CREATE TABLE \"UsuariosCuentasComerciales\"", sql);
        Assert.Contains("CREATE TABLE \"AspNetUserPasskeys\"", sql);
        Assert.Contains("CREATE TABLE \"SuscripcionesPush\"", sql);
        Assert.Contains("jsonb", sql);
        Assert.Contains("integer[]", sql);
        Assert.Contains("timestamp with time zone", sql);
        Assert.Contains("WHERE \"Activa\" = TRUE", sql);
        Assert.Contains("lower(\"Email\")", sql);
        Assert.DoesNotContain("ON DELETE CASCADE", sql);
    }

    [Fact]
    public void ConservaPropiedadExclusivaYParadaActivaSinColeccionHistoricaEnEnvio()
    {
        using var db = Context();
        var envio = db.Model.FindEntityType(typeof(Envio))!;
        Assert.True(envio.FindNavigation(nameof(Envio.Destinatario))!.ForeignKey.IsUnique);
        Assert.True(envio.FindNavigation(nameof(Envio.Parada))!.ForeignKey.IsUnique);
        Assert.False(envio.FindNavigation(nameof(Envio.Parada))!.ForeignKey.IsRequired);
        Assert.Null(envio.FindNavigation("Paradas"));
        Assert.True(db.Model.FindEntityType(typeof(Destinatario))!
            .FindNavigation(nameof(Destinatario.Direccion))!.ForeignKey.IsUnique);
        Assert.True(db.Model.FindEntityType(typeof(ReglasOperativas))!
            .FindNavigation(nameof(ReglasOperativas.PoliticaDevolucion))!.ForeignKey.IsUnique);
        Assert.True(envio.FindNavigation(nameof(Envio.Devolucion))!.ForeignKey.IsUnique);
        var parada = db.Model.FindEntityType(typeof(Parada))!;
        Assert.Contains(parada.GetIndexes(), i => i.IsUnique && i.GetFilter() == "\"Activa\" = TRUE");
    }

    [Fact]
    public void IdempotenciaYVersionesTienenIndicesPersistentes()
    {
        using var db = Context();
        void Unique<T>(params string[] properties) => Assert.Contains(db.Model.FindEntityType(typeof(T))!.GetIndexes(),
            i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(properties));
        Unique<CuentaComercial>("OperadorKey", "ComercioKey");
        Unique<ReglasOperativas>("OperadorKey", "Version");
        Unique<CuadroTarifario>("OperadorKey", "Version");
        Unique<PlazoServicio>("ReglasKey", "ModalidadServicio");
        Unique<IntentoEntrega>("EnvioKey", "IdOperacion");
        Unique<IntentoEntrega>("EnvioKey", "Numero");
        Unique<EntregaAviso>("SuscripcionKey", "EventoKey");
    }

    [Fact]
    public void ConsultaPorOperadorSeTraduceSinAbrirConexion()
    {
        using var db = Context();
        var operadorId = Guid.NewGuid();
        var sql = db.Envios.Where(e => e.CuentaComercial.Operador.Id == operadorId)
            .Select(e => new { e.Id, e.Estado }).ToQueryString();
        Assert.Contains("\"CuentasComerciales\"", sql);
        Assert.Contains("TenantId", sql);
        Assert.Contains("WHERE", sql);
        Assert.Contains("OperadorId", sql);
    }

    [Fact]
    public void MigracionesRegistradasCoincidenConElModeloActual()
    {
        using var db = Context();
        Assert.NotEmpty(db.Database.GetMigrations());
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
