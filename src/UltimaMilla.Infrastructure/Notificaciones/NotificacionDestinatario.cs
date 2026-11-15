using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Notificaciones;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Infrastructure.Notificaciones;

public sealed class OpcionesNotificaciones
{
    public const string Seccion = "Notificaciones";
    // Base pública del sitio para el enlace de seguimiento (por ejemplo https://dominio). Sin ella, ruta relativa.
    public string? UrlPublica { get; set; }
}

// Aviso al destinatario por cada cambio de estado relevante (docs/notificaciones-destinatario.md).
// - Idempotente con su propia clave (MensajeId, "notificaciones"): si después fallan los webhooks y el mensaje se
//   reintenta, el destinatario no recibe el aviso dos veces.
// - Primero se envía y después se guarda la Notificacion ya marcada como enviada. Si el envío falla no se guarda
//   nada, la reserva de idempotencia se deshace y el mensaje se reintenta por la cola. Si fallara el guardado (o la
//   confirmación de la reserva) después de enviar, el reintento lo reenvía: al menos una vez, nunca cero.
// Corre con el TenantContext del mensaje: solo ve y escribe datos de esa cuenta.
public sealed class NotificacionDestinatario(DbContextOptions<AppDbContext> opciones, IRegistroIdempotencia idempotencia,
    IEnviadorNotificaciones enviador, OpcionesNotificaciones configuracion, TimeProvider reloj,
    ILogger<NotificacionDestinatario> logger) : IManejadorEventoEnvio
{
    public const string Consumidor = "notificaciones";

    public async Task ManejarAsync(EventoEnvioV1 evento, TenantContext tenant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);
        ArgumentNullException.ThrowIfNull(tenant);
        if (!Enum.TryParse<EstadoEnvio>(evento.EstadoNuevo, ignoreCase: false, out var nuevo) || !Enum.IsDefined(nuevo)) return;
        EstadoEnvio? anterior = Enum.TryParse<EstadoEnvio>(evento.EstadoAnterior, ignoreCase: false, out var a) ? a : null;
        if (!MensajesDestinatario.Relevantes.Contains(nuevo) || anterior == nuevo) return;

        await idempotencia.EjecutarUnaVezAsync(tenant, evento.MensajeId, Consumidor,
            ct => AvisarAsync(evento, tenant, anterior, nuevo, ct), cancellationToken);
    }

    private async Task AvisarAsync(EventoEnvioV1 evento, TenantContext tenant, EstadoEnvio? anterior, EstadoEnvio nuevo,
        CancellationToken cancellationToken)
    {
        await using var db = new AppDbContext(opciones, new TenantActual(tenant));
        var envio = await db.Envios
            .Include(e => e.Destinatario).ThenInclude(d => d.Notificaciones)
            .Include(e => e.Programacion)
            .Include(e => e.CuentaComercial).ThenInclude(c => c.Operador)
            .SingleOrDefaultAsync(e => e.Id == evento.EnvioId, cancellationToken);
        if (envio is null)
        {
            logger.LogWarning("Envío del evento {EventoId} no encontrado para el operador; no se avisa.", evento.EventoId);
            return;
        }

        var operador = envio.CuentaComercial.Operador;
        var datos = new DatosAvisoDestinatario(operador.Marca, operador.EmailContacto, operador.TelefonoContacto,
            envio.Destinatario.Nombre, envio.CodigoSeguimiento, envio.Programacion.InicioLocal, envio.Programacion.FinLocal);
        var saliente = MensajesDestinatario.Componer(anterior, nuevo, datos, envio.Destinatario.Email, configuracion.UrlPublica);
        if (saliente is null) return;

        await enviador.EnviarAsync(saliente, cancellationToken);
        var notificacion = new Notificacion(envio.Destinatario, saliente.Canal, saliente.Cuerpo);
        // El dominio no acepta un envío anterior a la admisión (relojes desfasados entre procesos).
        notificacion.MarcarEnviada(new[] { reloj.GetUtcNow().UtcDateTime, envio.FechaAdmision }.Max());
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Aviso {Estado} enviado al destinatario del envío {EnvioId} ({EventoId}).",
            nuevo, envio.Id, evento.EventoId);
    }
}

// Sin proveedor de correo configurado: el aviso queda en el log (sin datos personales) y se considera enviado.
// Un proveedor real (SMTP, SES, SMS) se agrega implementando IEnviadorNotificaciones.
public sealed class EnviadorNotificacionesRegistro(ILogger<EnviadorNotificacionesRegistro> logger) : IEnviadorNotificaciones
{
    public Task EnviarAsync(NotificacionSaliente notificacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notificacion);
        logger.LogInformation("Notificación {Canal} a {Destino}: {Asunto}", notificacion.Canal, Enmascarar(notificacion.Destino),
            notificacion.Asunto);
        return Task.CompletedTask;
    }

    // "persona@example.com" -> "p***@example.com": el log no guarda el email completo del destinatario.
    public static string Enmascarar(string destino)
    {
        var arroba = destino.IndexOf('@');
        return arroba <= 0 ? "***" : destino[0] + "***" + destino[arroba..];
    }
}

// Corre todos los manejadores del evento. Cada uno es idempotente por su cuenta, así que si uno falla se corren
// igual los demás y al final se relanza el primer error para que el mensaje se reintente por la cola.
public sealed class ManejadoresEventoEnvio(IReadOnlyList<IManejadorEventoEnvio> manejadores) : IManejadorEventoEnvio
{
    public async Task ManejarAsync(EventoEnvioV1 evento, TenantContext tenant, CancellationToken cancellationToken = default)
    {
        Exception? primero = null;
        foreach (var manejador in manejadores)
        {
            try { await manejador.ManejarAsync(evento, tenant, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception error) { primero ??= error; }
        }
        if (primero is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primero).Throw();
    }
}
