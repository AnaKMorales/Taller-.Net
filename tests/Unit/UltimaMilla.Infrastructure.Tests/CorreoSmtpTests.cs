using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Application.Notificaciones;
using UltimaMilla.Infrastructure.Notificaciones;

namespace UltimaMilla.Infrastructure.Tests;

public sealed class CorreoSmtpTests
{
    private static readonly NotificacionSaliente Aviso = new(CanalNotificacion.Email, "persona@example.com",
        "Tu envío está en camino", "Hola Persona,\nseguilo en https://ejemplo/seguimiento/ABC123");

    private static OpcionesSmtp Opciones(int puerto) => new()
    {
        Servidor = "127.0.0.1", Puerto = puerto, Seguridad = SeguridadSmtp.Ninguna,
        Remitente = "avisos@ultimamilla.test", NombreRemitente = "Operador Demo", TimeoutSegundos = 5,
    };

    [Fact]
    public void ElMensajeLlevaRemitenteDestinoAsuntoYCuerpo()
    {
        using var mensaje = EnviadorNotificacionesSmtp.ArmarMensaje(Aviso, Opciones(25));

        Assert.Equal("\"Operador Demo\" <avisos@ultimamilla.test>", mensaje.From.ToString());
        Assert.Equal("persona@example.com", mensaje.To.ToString());
        Assert.Equal(Aviso.Asunto, mensaje.Subject);
        Assert.Equal(Aviso.Cuerpo, mensaje.TextBody);
    }

    [Fact]
    public async Task EnviaElCorreoAlServidorSmtp()
    {
        using var servidor = new ServidorSmtpFalso();

        await new EnviadorNotificacionesSmtp(Opciones(servidor.Puerto), NullLogger<EnviadorNotificacionesSmtp>.Instance)
            .EnviarAsync(Aviso);

        var recibido = await servidor.Recibido.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("RCPT TO:<persona@example.com>", recibido.Comandos);
        Assert.Contains("Subject:", recibido.Datos);
        Assert.Contains("ABC123", recibido.Datos);
    }

    [Fact]
    public async Task SiElServidorNoRespondeLanzaParaQueSeReintente()
    {
        var libre = new TcpListener(IPAddress.Loopback, 0);
        libre.Start();
        var puerto = ((IPEndPoint)libre.LocalEndpoint).Port;
        libre.Stop(); // nadie escucha en ese puerto

        await Assert.ThrowsAnyAsync<Exception>(() =>
            new EnviadorNotificacionesSmtp(Opciones(puerto), NullLogger<EnviadorNotificacionesSmtp>.Instance).EnviarAsync(Aviso));
    }

    [Fact]
    public async Task OtroCanalQueNoEsEmailNoSeEnviaPorSmtp()
    {
        var sms = Aviso with { Canal = CanalNotificacion.Sms, Destino = "+5491100000000" };
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            new EnviadorNotificacionesSmtp(Opciones(25), NullLogger<EnviadorNotificacionesSmtp>.Instance).EnviarAsync(sms));
    }

    [Fact]
    public void ConServidorSeUsaSmtpYSinServidorQuedaElRegistro()
    {
        using var conSmtp = new ServiceCollection().AddLogging()
            .AgregarNotificacionesDestinatario(smtp: Opciones(25)).BuildServiceProvider();
        using var sinSmtp = new ServiceCollection().AddLogging()
            .AgregarNotificacionesDestinatario(smtp: new OpcionesSmtp()).BuildServiceProvider();

        Assert.IsType<EnviadorNotificacionesSmtp>(conSmtp.GetRequiredService<IEnviadorNotificaciones>());
        Assert.IsType<EnviadorNotificacionesRegistro>(sinSmtp.GetRequiredService<IEnviadorNotificaciones>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("no-es-un-email")]
    public void ConServidorPeroSinRemitenteValidoFallaAlArrancar(string? remitente)
    {
        var opciones = Opciones(25);
        opciones.Remitente = remitente;
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AgregarNotificacionesDestinatario(smtp: opciones));
    }

    [Fact]
    public void UsuarioSinClaveFallaAlArrancar()
    {
        var opciones = Opciones(25);
        opciones.Usuario = "usuario";
        Assert.Throws<InvalidOperationException>(opciones.Validar);
    }

    private sealed record Correo(string Comandos, string Datos);

    // Servidor SMTP mínimo (sin TLS ni autenticación) que acepta un solo correo y lo devuelve.
    private sealed class ServidorSmtpFalso : IDisposable
    {
        private readonly TcpListener escucha = new(IPAddress.Loopback, 0);
        private readonly TaskCompletionSource<Correo> recibido = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ServidorSmtpFalso()
        {
            escucha.Start();
            _ = Task.Run(AtenderAsync);
        }

        public int Puerto => ((IPEndPoint)escucha.LocalEndpoint).Port;
        public Task<Correo> Recibido => recibido.Task;

        private async Task AtenderAsync()
        {
            try
            {
                using var cliente = await escucha.AcceptTcpClientAsync();
                using var flujo = cliente.GetStream();
                using var lector = new StreamReader(flujo, Encoding.ASCII);
                await using var escritor = new StreamWriter(flujo, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                var comandos = new StringBuilder();
                var datos = new StringBuilder();

                await escritor.WriteLineAsync("220 falso ESMTP");
                while (await lector.ReadLineAsync() is { } linea)
                {
                    comandos.AppendLine(linea);
                    var verbo = linea.Split(' ')[0].ToUpperInvariant();
                    if (verbo is "EHLO" or "HELO") await escritor.WriteLineAsync("250 falso");
                    else if (verbo == "DATA")
                    {
                        await escritor.WriteLineAsync("354 seguir");
                        while (await lector.ReadLineAsync() is { } dato && dato != ".") datos.AppendLine(dato);
                        await escritor.WriteLineAsync("250 aceptado");
                    }
                    else if (verbo == "QUIT")
                    {
                        await escritor.WriteLineAsync("221 chau");
                        break;
                    }
                    else await escritor.WriteLineAsync("250 ok");
                }
                recibido.TrySetResult(new Correo(comandos.ToString(), datos.ToString()));
            }
            catch (Exception error)
            {
                recibido.TrySetException(error);
            }
        }

        public void Dispose() => escucha.Stop();
    }
}
