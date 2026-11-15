namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

// Las contraseñas viven en ASP.NET Identity (AspNetUsers.PasswordHash, ver Identidad/FabricaCuentasAcceso).
// La columna ContrasenaHash de Usuarios queda solo como respaldo de la migración AgregarIdentidad: ya no se escribe
// ni se lee, y se borrará en una migración posterior (ADR-008).
public static class ContrasenasUsuario
{
    public const string Columna = "ContrasenaHash";
    public const int LongitudMinima = 8;
}

internal sealed class CredencialesConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder) =>
        builder.Property<string?>(ContrasenasUsuario.Columna).HasMaxLength(256);
}
