using Microsoft.AspNetCore.Identity;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

// Hash de la contraseña (PBKDF2, como ASP.NET Identity) en una columna shadow de Usuario.
public static class ContrasenasUsuario
{
    public const string Columna = "ContrasenaHash";
    public const int LongitudMinima = 8;

    internal static readonly PasswordHasher<Usuario> Hasher = new();

    public static void Asignar(AppDbContext db, Usuario usuario, string contrasena)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < LongitudMinima)
            throw new ArgumentException($"La contraseña debe tener al menos {LongitudMinima} caracteres.", nameof(contrasena));
        db.Entry(usuario).Property<string?>(Columna).CurrentValue = Hasher.HashPassword(usuario, contrasena);
    }
}

internal sealed class CredencialesConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder) =>
        builder.Property<string?>(ContrasenasUsuario.Columna).HasMaxLength(256);
}
