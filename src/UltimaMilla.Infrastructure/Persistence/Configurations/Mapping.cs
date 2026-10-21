using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace UltimaMilla.Infrastructure.Persistence.Configurations;

internal static class Mapping
{
    internal static void Entity<T>(EntityTypeBuilder<T> b, string table) where T : Entidad
    {
        b.ToTable(table);
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
    }

    internal static void Key<T>(EntityTypeBuilder<T> b, string navigation, bool optional = false,
        string? column = null) where T : class
    {
        b.Property(optional ? typeof(Guid?) : typeof(Guid), navigation + "Key")
            .HasColumnName(column ?? navigation + "Id");
    }

    internal static void Collection<T>(EntityTypeBuilder<T> b, string navigation, string field) where T : class =>
        b.Navigation(navigation).HasField(field).UsePropertyAccessMode(PropertyAccessMode.Field);

    internal static void EnumSet<TEnum>(PropertyBuilder<IReadOnlySet<TEnum>> property)
        where TEnum : struct, Enum
    {
        property.HasConversion(new ValueConverter<IReadOnlySet<TEnum>, int[]>(
            set => set.Select(value => Convert.ToInt32(value)).OrderBy(value => value).ToArray(),
            values => values.Select(value => (TEnum)Enum.ToObject(typeof(TEnum), value)).ToFrozenSet()));
        property.Metadata.SetValueComparer(new ValueComparer<IReadOnlySet<TEnum>>(
            (a, b) => a != null && b != null && a.SetEquals(b),
            set => set.Aggregate(0, (hash, value) => hash ^ value.GetHashCode()),
            set => set.ToFrozenSet()));
        property.HasColumnType("integer[]");
    }

    internal static readonly ValueConverter<GeoPunto, string> GeoConverter = new(
        value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
        value => JsonSerializer.Deserialize<GeoPunto>(value, (JsonSerializerOptions?)null)!);

    internal static readonly ValueConverter<ArchivoRef, string> ArchivoConverter = new(
        value => value.Ubicacion, value => new ArchivoRef(value));

    // timestamptz conserva el instante, pero no el offset original elegido para la ventana.
    internal static readonly ValueConverter<DateTimeOffset, string> LocalDateConverter = new(
        value => value.ToString("O", CultureInfo.InvariantCulture),
        value => DateTimeOffset.ParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None));
}
