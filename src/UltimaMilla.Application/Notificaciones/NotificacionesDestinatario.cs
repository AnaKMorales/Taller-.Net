using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Notificaciones;

namespace UltimaMilla.Application.Notificaciones;

// Aviso al destinatario ante los cambios de estado relevantes (requisito 27), con la identidad visual del operador
// (requisito 5). Ver docs/notificaciones-destinatario.md.

public sealed record NotificacionSaliente(CanalNotificacion Canal, string Destino, string Asunto, string Cuerpo);

// Canal de salida (correo, SMS...). La implementación vive en Infrastructure.
public interface IEnviadorNotificaciones
{
    // Lanza una excepción si no se pudo entregar: el mensaje vuelve a la cola y se reintenta.
    Task EnviarAsync(NotificacionSaliente notificacion, CancellationToken cancellationToken = default);
}

// Datos del envío que necesita el texto. Las horas de la ventana ya vienen en hora local del operador.
public sealed record DatosAvisoDestinatario(string Marca, string? EmailContacto, string? TelefonoContacto,
    string NombreDestinatario, string CodigoSeguimiento, DateTimeOffset VentanaDesde, DateTimeOffset VentanaHasta);

// Texto del aviso según el estado nuevo. Puro: sin base de datos ni reloj.
public static class MensajesDestinatario
{
    // Estados que el destinatario necesita conocer. Los internos (en depósito, asignado a ruta) y el cierre de la
    // devolución (que importa al comercio, no al destinatario) no se avisan.
    public static readonly IReadOnlySet<EstadoEnvio> Relevantes = new HashSet<EstadoEnvio>
    {
        EstadoEnvio.Admitido, EstadoEnvio.EnTransito, EstadoEnvio.Entregado, EstadoEnvio.EntregadoParcialmente,
        EstadoEnvio.NoEntregado, EstadoEnvio.Reprogramado, EstadoEnvio.EnDevolucion, EstadoEnvio.Extraviado
    };

    // null si el cambio no se avisa: estado no relevante o sin cambio real de estado.
    public static NotificacionSaliente? Componer(EstadoEnvio? anterior, EstadoEnvio nuevo, DatosAvisoDestinatario datos,
        string destino, string? urlPublica)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (!Relevantes.Contains(nuevo) || anterior == nuevo) return null;
        var enlace = Enlace(urlPublica, datos.CodigoSeguimiento);
        var detalle = nuevo switch
        {
            EstadoEnvio.Admitido => $"registramos tu envío. Entrega prevista: {Ventana(datos)}.",
            EstadoEnvio.EnTransito => $"tu envío salió a reparto. Llega {Ventana(datos)}.",
            EstadoEnvio.Entregado => "tu envío fue entregado.",
            EstadoEnvio.EntregadoParcialmente => "entregamos parte de tu envío. Te vamos a avisar qué pasa con los bultos pendientes.",
            EstadoEnvio.NoEntregado => "no pudimos entregar tu envío. Desde el seguimiento podés pedir otra fecha.",
            EstadoEnvio.Reprogramado => $"reprogramamos tu entrega para {Ventana(datos)}.",
            EstadoEnvio.EnDevolucion => "tu envío vuelve al comercio que lo despachó.",
            EstadoEnvio.Extraviado => "tu envío tuvo un problema. El operador se va a comunicar con vos.",
            _ => throw new InvalidOperationException("Estado no relevante.")
        };
        var cuerpo = $"Hola {datos.NombreDestinatario}: {detalle}\nSeguimiento: {enlace}";
        var contacto = string.Join(" · ", new[] { datos.EmailContacto, datos.TelefonoContacto }.Where(c => !string.IsNullOrWhiteSpace(c)));
        if (contacto.Length > 0) cuerpo += $"\nConsultas a {datos.Marca}: {contacto}";
        return new(CanalNotificacion.Email, destino, $"{datos.Marca}: {Asunto(nuevo)}", cuerpo);
    }

    private static string Asunto(EstadoEnvio estado) => estado switch
    {
        EstadoEnvio.Admitido => "registramos tu envío",
        EstadoEnvio.EnTransito => "tu envío está en camino",
        EstadoEnvio.Entregado => "tu envío fue entregado",
        EstadoEnvio.EntregadoParcialmente => "entrega parcial de tu envío",
        EstadoEnvio.NoEntregado => "no pudimos entregar tu envío",
        EstadoEnvio.Reprogramado => "reprogramamos tu entrega",
        EstadoEnvio.EnDevolucion => "tu envío vuelve al comercio",
        _ => "novedades de tu envío"
    };

    // Sin URL pública configurada queda la ruta relativa: igual identifica el seguimiento.
    private static string Enlace(string? urlPublica, string codigo) =>
        (string.IsNullOrWhiteSpace(urlPublica) ? "" : urlPublica.TrimEnd('/')) + "/seguimiento/" + codigo;

    private static string Ventana(DatosAvisoDestinatario d) =>
        $"el {d.VentanaDesde:dd/MM} entre las {d.VentanaDesde:HH:mm} y las {d.VentanaHasta:HH:mm}";
}
