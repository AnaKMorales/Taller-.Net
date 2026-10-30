using Microsoft.AspNetCore.Identity;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

// La contraseña es un dato de seguridad, no del negocio: vive como propiedad shadow de Usuario
// (columna en la tabla, sin propiedad en la clase de dominio). Se guarda solo el hash, con el mismo
// algoritmo que usa ASP.NET Identity (PBKDF2 con sal), así se puede migrar a Identity sin rehacer las claves.
public static class ContrasenasUsuario
{
    public const string Columna = "ContrasenaHash";
    public const int LongitudMinima = 8;

    internal static readonly PasswordHasher<Usuario> Hasher = new();

    // El usuario tiene que estar en el contexto (recién agregado o leído con seguimiento).
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
