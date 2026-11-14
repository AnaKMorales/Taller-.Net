namespace UltimaMilla.Domain.Tests;

public sealed class IdentidadOperadorTests
{
  [Fact]
  public void ConfiguracionValidaNormalizaDatosSinCambiarRazonSocial()
  {
    var op = new Operador("Razón legal"); var version = op.VersionIdentidad;
    Assert.Equal(op.RazonSocial, op.Marca);
    op.ConfigurarIdentidad(" Marca ", " #aabbcc ", "#123456", " 099123456 ", " CONTACTO@example.com ", "https://marca.example/logo.png");
    Assert.Equal("Marca", op.Marca); Assert.Equal("Razón legal", op.RazonSocial);
    Assert.Equal("#AABBCC", op.ColorPrimario); Assert.Equal("099123456", op.TelefonoContacto);
    Assert.Equal("contacto@example.com", op.EmailContacto); Assert.NotEqual(version, op.VersionIdentidad);
    op.ConfigurarIdentidad("Marca", "#000000", "#FFFFFF", " ", null, "");
    Assert.Null(op.TelefonoContacto); Assert.Null(op.EmailContacto); Assert.Null(op.LogoUrl);
  }

  [Theory]
  [InlineData("", "#123456", "#FFFFFF", null, null, null)]
  [InlineData("Marca", "red", "#FFFFFF", null, null, null)]
  [InlineData("Marca", "#123456", "#XYZXYZ", null, null, null)]
  [InlineData("Marca", "#123456", "#FFFFFF", null, "no-email", null)]
  [InlineData("Marca", "#123456", "#FFFFFF", null, "Nombre <a@example.com>", null)]
  [InlineData("Marca", "#123456", "#FFFFFF", null, null, "javascript:alert(1)")]
  [InlineData("Marca", "#123456", "#FFFFFF", null, null, "http://marca.example/logo.png")]
  [InlineData("Marca", "#123456", "#FFFFFF", null, null, "https://user:secret@marca.example/logo.png")]
  public void DatosInvalidosNoCambianElEstado(string marca, string primario, string secundario, string? tel, string? email, string? logo)
  {
    var op = new Operador("Original"); var version = op.VersionIdentidad;
    Assert.ThrowsAny<ArgumentException>(() => op.ConfigurarIdentidad(marca, primario, secundario, tel, email, logo));
    Assert.Equal("Original", op.Marca); Assert.Equal(version, op.VersionIdentidad);
    Assert.Null(op.EmailContacto); Assert.Null(op.LogoUrl);
  }
}
