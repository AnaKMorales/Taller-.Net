using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Push;

namespace UltimaMilla.Infrastructure.Tests;

// Avisos push al repartidor (docs/push-repartidor.md): cifrado RFC 8291, firma VAPID RFC 8292 y configuración.
public sealed class WebPushTests
{
    private static byte[] B64(string texto)
    {
        var b64 = texto.Replace(" ", "").Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b64 + new string('=', (4 - b64.Length % 4) % 4));
    }

    private static string B64Url(byte[] datos) => Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static OpcionesWebPush ClavesNuevas()
    {
        using var clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = clave.ExportParameters(true);
        return new OpcionesWebPush
        {
            ClavePublica = B64Url([0x04, .. p.Q.X!, .. p.Q.Y!]),
            ClavePrivada = B64Url(p.D!),
            Asunto = "mailto:pruebas@ultimamilla.local",
        };
    }

    [Fact]
    public void CifradoCoincideConElVectorDelRfc8291()
    {
        // RFC 8291, sección 5 y apéndice A.
        var servidor = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = B64("yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw"),
            Q = new ECPoint
            {
                X = B64("BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8")[1..33],
                Y = B64("BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8")[33..65],
            },
        });

        var cuerpo = CifradoWebPush.Cifrar("When I grow up, I want to be a watermelon"u8,
            B64("BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4"),
            B64("BTBZMqHH6r4Tts7J_aSIgg"), servidor, B64("DGv6ra1nlYgDCS1FRnbzlw"));

        Assert.Equal(B64("""
            DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27ml
            mlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPT
            pK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN
            """.Replace("\n", "").Replace("\r", "")), cuerpo);
    }

    [Fact]
    public void CadaAvisoUsaSaltYClaveEfimeraNuevos()
    {
        using var dispositivo = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p = dispositivo.ExportParameters(false);
        var p256dh = B64Url([0x04, .. p.Q.X!, .. p.Q.Y!]);
        var auth = B64Url(RandomNumberGenerator.GetBytes(16));

        var uno = CifradoWebPush.Cifrar("hola"u8, p256dh, auth);
        var otro = CifradoWebPush.Cifrar("hola"u8, p256dh, auth);

        Assert.Equal(86 + 4 + 1 + 16, uno.Length);
        Assert.NotEqual(uno[..16], otro[..16]);
        Assert.NotEqual(uno[21..86], otro[21..86]);
        Assert.Throws<ArgumentException>(() => CifradoWebPush.Cifrar(new byte[CifradoWebPush.MaximoContenido + 1], p256dh, auth));
    }

    [Fact]
    public void LaFirmaVapidEsUnJwtEs256VerificableConLaClavePublica()
    {
        var opciones = ClavesNuevas();
        var ahora = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

        var encabezado = FirmaVapid.Encabezado(new Uri("https://fcm.googleapis.com/fcm/send/abc"), opciones, ahora);

        Assert.StartsWith("vapid t=", encabezado);
        Assert.EndsWith($", k={opciones.ClavePublica}", encabezado);
        var jwt = encabezado["vapid t=".Length..encabezado.IndexOf(", k=", StringComparison.Ordinal)].Split('.');
        using var reclamos = JsonDocument.Parse(B64(jwt[1]));
        Assert.Equal("https://fcm.googleapis.com", reclamos.RootElement.GetProperty("aud").GetString());
        Assert.Equal(ahora.AddHours(12).ToUnixTimeSeconds(), reclamos.RootElement.GetProperty("exp").GetInt64());
        Assert.Equal("mailto:pruebas@ultimamilla.local", reclamos.RootElement.GetProperty("sub").GetString());
        var publica = B64(opciones.ClavePublica!);
        using var verificador = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = publica[1..33], Y = publica[33..65] },
        });
        Assert.True(verificador.VerifyData(Encoding.ASCII.GetBytes($"{jwt[0]}.{jwt[1]}"), B64(jwt[2]),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Fact]
    public void LaConfiguracionExigeUnParDeClavesValido()
    {
        new OpcionesWebPush().Validar(); // sin claves: apagado, sin error
        ClavesNuevas().Validar();
        new OpcionesWebPush { ClavePublica = ClavesNuevas().ClavePublica }.Validar(); // Web: solo la pública

        var mezcladas = ClavesNuevas();
        mezcladas.ClavePrivada = ClavesNuevas().ClavePrivada;
        Assert.Throws<InvalidOperationException>(mezcladas.Validar);
        Assert.Throws<InvalidOperationException>(new OpcionesWebPush { ClavePublica = "no-es-una-clave" }.Validar);
        var sinAsunto = ClavesNuevas();
        sinAsunto.Asunto = "soporte@ultimamilla.local";
        Assert.Throws<InvalidOperationException>(sinAsunto.Validar);
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [InlineData("https://web.push.apple.com/abc", true)]
    [InlineData("https://db5p.notify.windows.com/w/?token=abc", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com.example.com/abc", false)]
    [InlineData("https://user@fcm.googleapis.com/abc", false)]
    [InlineData("https://169.254.169.254/latest/meta-data", false)]
    [InlineData("https://localhost/abc", false)]
    public void SoloSeEnviaALosServiciosPushConocidos(string endpoint, bool permitido) =>
        Assert.Equal(permitido, new OpcionesWebPush().EsServicioPermitido(new Uri(endpoint)));

    private sealed class IdempotenciaQueCuenta : IRegistroIdempotencia
    {
        public int Llamadas { get; private set; }
        public Task<bool> EjecutarUnaVezAsync(TenantContext tenant, Guid mensajeId, string consumidor,
            Func<CancellationToken, Task> accion, CancellationToken cancellationToken = default)
        {
            Llamadas++;
            return Task.FromResult(true);
        }
    }

    private sealed class ClienteQueNoSeUsa : IClienteWebPush
    {
        public Task<ResultadoPush> EnviarAsync(Uri endpoint, string p256dh, string auth, byte[] contenido, string etiqueta,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("No debía enviar.");
    }

    [Theory]
    [InlineData("IniciarEntrega", true)]
    [InlineData("Reasignar", false)]
    public async Task SoloAvisaAsignacionesYConClavesConfiguradas(string tipo, bool conClaves)
    {
        var evento = new EventoEnvioV1(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            tipo, "Sistema", "EnDeposito", "AsignadoARuta", DateTime.UtcNow, null);
        var idempotencia = new IdempotenciaQueCuenta();
        var aviso = new AvisoPushRepartidor(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost").Options,
            idempotencia, new ClienteQueNoSeUsa(), conClaves ? ClavesNuevas() : new OpcionesWebPush(),
            NullLogger<AvisoPushRepartidor>.Instance);

        await aviso.ManejarAsync(evento, TenantContext.ParaMensaje(evento.OperadorId, evento.CuentaComercialId));

        Assert.Equal(0, idempotencia.Llamadas);
    }
}
