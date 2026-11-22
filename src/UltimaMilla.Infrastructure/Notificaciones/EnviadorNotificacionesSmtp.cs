using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using UltimaMilla.Application.Notificaciones;
using UltimaMilla.Domain.Notificaciones;

namespace UltimaMilla.Infrastructure.Notificaciones;

public enum SeguridadSmtp
{
    // STARTTLS obligatorio (puerto 587): lo normal para Gmail, SES, etc.
    StartTls,
    // TLS desde el primer byte (puerto 465).
    SslAlConectar,
    // Sin cifrado: SOLO para Mailpit en la red interna de Docker.
    Ninguna,
}

// Servidor SMTP para los avisos al destinatario. Sin Servidor, el Worker sigue usando EnviadorNotificacionesRegistro.
// La clave nunca va en appsettings: Smtp__Clave por variable de entorno (deploy/.env, SSM en AWS) o user-secrets.
public sealed class OpcionesSmtp
{
    public const string Seccion = "Smtp";
    public string? Servidor { get; set; }
    public int Puerto { get; set; } = 587;
    public SeguridadSmtp Seguridad { get; set; } = SeguridadSmtp.StartTls;
    // Sin usuario no se autentica (Mailpit).
    public string? Usuario { get; set; }
    public string? Clave { get; set; }
    public string? Remitente { get; set; }
    public string NombreRemitente { get; set; } = "UltimaMilla";
    public int TimeoutSegundos { get; set; } = 15;

    public bool Configurado => !string.IsNullOrWhiteSpace(Servidor);

    public void Validar()
    {
        if (!Configurado) return;
        if (string.IsNullOrWhiteSpace(Remitente) || !Remitente.Contains('@') || !MailboxAddress.TryParse(Remitente, out _))
            throw new InvalidOperationException("Smtp:Remitente falta o no es un email válido.");
        if (Puerto is <= 0 or > 65535) throw new InvalidOperationException("Smtp:Puerto fuera de rango.");
        if (!string.IsNullOrWhiteSpace(Usuario) && string.IsNullOrEmpty(Clave))
            throw new InvalidOperationException("Smtp:Usuario configurado sin Smtp:Clave.");
        if (TimeoutSegundos <= 0) throw new InvalidOperationException("Smtp:TimeoutSegundos debe ser mayor a cero.");
    }
}

// Manda el aviso por correo (SMTP, con MailKit). Una conexión por aviso: el cliente de MailKit no se comparte entre
// hilos y el volumen es bajo. Cualquier error se propaga: el mensaje vuelve a la cola y se reintenta (5 s, 30 s, 2 min, DLQ).
public sealed class EnviadorNotificacionesSmtp(OpcionesSmtp opciones, ILogger<EnviadorNotificacionesSmtp> logger)
    : IEnviadorNotificaciones
{
    public async Task EnviarAsync(NotificacionSaliente notificacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notificacion);
        if (notificacion.Canal != CanalNotificacion.Email)
            throw new NotSupportedException($"El canal {notificacion.Canal} no se envía por SMTP.");

        using var mensaje = ArmarMensaje(notificacion, opciones);
        // Servidor, Remitente y Clave ya los comprobó OpcionesSmtp.Validar al arrancar.
        using var cliente = new SmtpClient { Timeout = opciones.TimeoutSegundos * 1000 };
        var seguridad = opciones.Seguridad switch
        {
            SeguridadSmtp.SslAlConectar => SecureSocketOptions.SslOnConnect,
            SeguridadSmtp.Ninguna => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTls,
        };

        await cliente.ConnectAsync(opciones.Servidor!, opciones.Puerto, seguridad, cancellationToken);
        if (!string.IsNullOrWhiteSpace(opciones.Usuario))
            await cliente.AuthenticateAsync(opciones.Usuario, opciones.Clave!, cancellationToken);
        await cliente.SendAsync(mensaje, cancellationToken);
        await cliente.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("Aviso por correo enviado a {Destino}: {Asunto}",
            EnviadorNotificacionesRegistro.Enmascarar(notificacion.Destino), notificacion.Asunto);
    }

    // Texto plano: el cuerpo ya viene armado por MensajesDestinatario (marca, ventana y enlace de seguimiento).
    public static MimeMessage ArmarMensaje(NotificacionSaliente notificacion, OpcionesSmtp opciones)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(opciones.NombreRemitente, opciones.Remitente!));
        mensaje.To.Add(MailboxAddress.Parse(notificacion.Destino));
        mensaje.Subject = notificacion.Asunto;
        mensaje.Body = new TextPart("plain") { Text = notificacion.Cuerpo };
        return mensaje;
    }
}
