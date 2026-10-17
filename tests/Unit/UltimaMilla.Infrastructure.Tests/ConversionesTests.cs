namespace UltimaMilla.Infrastructure.Tests;

public sealed class ConversionesTests
{
    [Fact]
    public void VentanaConservaOffsetOriginalYMicrosegundos()
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        var converter = db.Model.FindEntityType(typeof(ProgramacionEntrega))!
            .FindProperty(nameof(ProgramacionEntrega.InicioLocal))!.GetTypeMapping().Converter!;
        var original = new DateTimeOffset(2026, 10, 14, 8, 0, 0, TimeSpan.FromHours(-3)).AddTicks(1234567);
        var actual = Assert.IsType<DateTimeOffset>(converter.ConvertFromProvider(converter.ConvertToProvider(original)));
        Assert.True(original.EqualsExact(actual));
    }

    [Fact]
    public void ConjuntoDePruebasSeSerializaSinDependerDelOrden()
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        var property = db.Model.FindEntityType(typeof(ReglasOperativas))!.FindProperty(nameof(ReglasOperativas.TiposPrueba))!;
        var converter = property.GetTypeMapping().Converter!;
        IReadOnlySet<TipoPrueba> original = new HashSet<TipoPrueba> { TipoPrueba.Nombre, TipoPrueba.Foto };
        var values = Assert.IsType<int[]>(converter.ConvertToProvider(original));
        Assert.Equal(values.Order(), values);
        var actual = Assert.IsAssignableFrom<IReadOnlySet<TipoPrueba>>(converter.ConvertFromProvider(values));
        Assert.True(original.SetEquals(actual));
        Assert.True(property.GetValueComparer()!.Equals(original, actual));
    }

    [Fact]
    public void GeolocalizacionYArchivoVuelvenComoValoresDeDomain()
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        var type = db.Model.FindEntityType(typeof(PruebaEntrega))!;
        var geo = type.FindProperty(nameof(PruebaEntrega.Posicion))!.GetTypeMapping().Converter!;
        var file = type.FindProperty(nameof(PruebaEntrega.ArchivoRef))!.GetTypeMapping().Converter!;
        var punto = new GeoPunto(-34.9011m, -56.1645m);
        var archivo = new ArchivoRef("evidencias/entrega.jpg");
        Assert.Equal(punto, geo.ConvertFromProvider(geo.ConvertToProvider(punto)));
        Assert.Equal(archivo, file.ConvertFromProvider(file.ConvertToProvider(archivo)));
    }
}
