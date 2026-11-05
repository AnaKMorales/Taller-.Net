using UltimaMilla.Application.Avisos;

namespace UltimaMilla.Application.Tests;

public sealed class FirmaWebhookTests
{
    // Vectores calculados aparte con Python: hmac.new(secreto, f"{timestamp}.{cuerpo}", sha256).hexdigest().
    [Theory]
    [InlineData("secreto-demo", 1760000000L, """{"a":1}""", "4131498eaec615ee409e740ad0c3d0594b15d39bdcd26a2292c285d9e1f16f79")]
    [InlineData("clave", 1L, "", "0f3941b6b4118c1d9994a2cf408842f4c56f105c877b95492c530b64b61475fe")]
    [InlineData("sécret0", 1760000123L, """{"nombre":"Peña – envío ñandú"}""", "44e2945a6b0e091b41f282e542e07d7a87ac19aa69ee62f56961e43132d9e0f0")]
    public void LaFirmaCoincideConElHmacSha256DeTimestampPuntoCuerpo(string secreto, long timestamp, string cuerpo, string hex) =>
        Assert.Equal("sha256=" + hex, FirmaWebhook.Calcular(secreto, timestamp, cuerpo));

    [Fact]
    public void CualquierCambioDeSecretoTimestampOCuerpoCambiaLaFirma()
    {
        var base_ = FirmaWebhook.Calcular("s", 100, "cuerpo");
        Assert.NotEqual(base_, FirmaWebhook.Calcular("S", 100, "cuerpo"));
        Assert.NotEqual(base_, FirmaWebhook.Calcular("s", 101, "cuerpo"));
        Assert.NotEqual(base_, FirmaWebhook.Calcular("s", 100, "cuerpo "));
        Assert.Equal(base_, FirmaWebhook.Calcular("s", 100, "cuerpo"));
    }

    [Fact]
    public void ElTimestampQuedaAtadoAlCuerpoPorElPunto()
    {
        // "1" + ".2" + cuerpo vs "1.2" + cuerpo: el separador evita confundir timestamp y cuerpo.
        Assert.NotEqual(FirmaWebhook.Calcular("s", 1, "2.x"), FirmaWebhook.Calcular("s", 12, ".x"));
    }

    [Fact]
    public void VerificaUnaFirmaValidaDentroDeLaVentana()
    {
        var ahora = DateTimeOffset.FromUnixTimeSeconds(1760000100);
        var firma = FirmaWebhook.Calcular("s", 1760000000, "{}");
        Assert.True(FirmaWebhook.Verificar("s", "1760000000", "{}", firma, ahora, TimeSpan.FromMinutes(5)));
    }

    [Theory]
    [InlineData("otro-secreto", "1760000000", "{}", false)]   // secreto distinto
    [InlineData("s", "1760000000", """{"x":1}""", false)]     // cuerpo alterado
    [InlineData("s", "1760000001", "{}", false)]              // timestamp alterado
    [InlineData("s", "no-numero", "{}", false)]               // timestamp inválido
    public void RechazaFirmasAlteradas(string secretoReceptor, string timestamp, string cuerpo, bool esperado)
    {
        var firma = FirmaWebhook.Calcular("s", 1760000000, "{}");
        Assert.Equal(esperado, FirmaWebhook.Verificar(secretoReceptor, timestamp, cuerpo, firma,
            DateTimeOffset.FromUnixTimeSeconds(1760000100), TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void RechazaUnReenvioFueraDeLaVentanaAunqueLaFirmaSeaValida()
    {
        var firma = FirmaWebhook.Calcular("s", 1760000000, "{}");
        var muchoDespues = DateTimeOffset.FromUnixTimeSeconds(1760000000 + 3600);
        Assert.False(FirmaWebhook.Verificar("s", "1760000000", "{}", firma, muchoDespues, TimeSpan.FromMinutes(5)));
        var muchoAntes = DateTimeOffset.FromUnixTimeSeconds(1760000000 - 3600);
        Assert.False(FirmaWebhook.Verificar("s", "1760000000", "{}", firma, muchoAntes, TimeSpan.FromMinutes(5)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha256=")]
    [InlineData("sha256=00")]
    public void RechazaFirmasAusentesOMalFormadas(string? firma) =>
        Assert.False(FirmaWebhook.Verificar("s", "1760000000", "{}", firma, DateTimeOffset.FromUnixTimeSeconds(1760000000), TimeSpan.FromMinutes(5)));

    [Fact]
    public void ExigeUnSecreto()
    {
        Assert.Throws<ArgumentException>(() => FirmaWebhook.Calcular("", 1, "x"));
        Assert.Throws<ArgumentNullException>(() => FirmaWebhook.Calcular("s", 1, null!));
    }
}
