using UltimaMilla.Application.Portal;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Web.Compartido;

public static class Textos
{
    public static readonly IReadOnlyDictionary<EstadoEnvio, string> Estados = new Dictionary<EstadoEnvio, string>
    {
        [EstadoEnvio.Admitido] = "Admitido",
        [EstadoEnvio.EnDeposito] = "En depósito",
        [EstadoEnvio.AsignadoARuta] = "Asignado a ruta",
        [EstadoEnvio.EnTransito] = "En tránsito",
        [EstadoEnvio.Entregado] = "Entregado",
        [EstadoEnvio.NoEntregado] = "No entregado",
        [EstadoEnvio.Reprogramado] = "Reprogramado",
        [EstadoEnvio.EnDevolucion] = "En devolución",
        [EstadoEnvio.Devuelto] = "Devuelto",
        [EstadoEnvio.Extraviado] = "Extraviado",
        [EstadoEnvio.EntregadoParcialmente] = "Entregado parcialmente"
    };

    public static readonly IReadOnlyDictionary<TipoEvento, string> Eventos = new Dictionary<TipoEvento, string>
    {
        [TipoEvento.Admitir] = "Alta",
        [TipoEvento.Validar] = "Recepción en depósito",
        [TipoEvento.Asignar] = "Asignación a ruta",
        [TipoEvento.Rechazar] = "Retiro de ruta",
        [TipoEvento.Aplazar] = "Aplazado",
        [TipoEvento.Reasignar] = "Reasignación",
        [TipoEvento.IniciarEntrega] = "Salida a reparto",
        [TipoEvento.EntregaExitosa] = "Entrega exitosa",
        [TipoEvento.FalloEntrega] = "Entrega fallida",
        [TipoEvento.Reintentar] = "Reintento programado",
        [TipoEvento.Devolver] = "Inicio de devolución",
        [TipoEvento.Cancelar] = "Cancelación",
        [TipoEvento.DevolucionExitosa] = "Devolución completada",
        [TipoEvento.EventoAnomalo] = "Extravío",
        [TipoEvento.EntregaParcial] = "Entrega parcial"
    };

    public static string Estado(EstadoEnvio? estado) => estado is { } e ? Estados[e] : "—";

    public static string Bulto(EstadoBulto estado) => estado switch
    {
        EstadoBulto.Admitido => "Admitido",
        EstadoBulto.EnDeposito => "En depósito",
        EstadoBulto.AsignadoARuta => "Asignado a ruta",
        EstadoBulto.EnTransito => "En tránsito",
        EstadoBulto.Entregado => "Entregado",
        EstadoBulto.NoEntregado => "No entregado (pendiente)",
        EstadoBulto.Reprogramado => "Reprogramado (pendiente)",
        EstadoBulto.EnDevolucion => "En devolución",
        EstadoBulto.Devuelto => "Devuelto",
        EstadoBulto.Extraviado => "Extraviado",
        _ => estado.ToString()
    };

    public static string Resultado(ResultadoIntento resultado) => resultado switch
    {
        ResultadoIntento.Entregado => "Entregado",
        ResultadoIntento.Fallido => "No entregado",
        ResultadoIntento.Parcial => "Parcial",
        _ => resultado.ToString()
    };

    public static string Pendientes(UltimaMilla.Domain.Configuracion.AccionBultosPendientes accion) => accion switch
    {
        UltimaMilla.Domain.Configuracion.AccionBultosPendientes.NuevaVisitaSinRecargo => "Nueva visita sin recargo",
        UltimaMilla.Domain.Configuracion.AccionBultosPendientes.NuevaVisitaConRecargo => "Nueva visita con recargo",
        UltimaMilla.Domain.Configuracion.AccionBultosPendientes.DevolverAlComercio => "Devolver al comercio",
        _ => accion.ToString()
    };

    public static string Evento(TipoEvento tipo) => Eventos[tipo];

    public static string Aviso(EstadoAviso estado) => estado switch
    {
        EstadoAviso.Entregado => "Entregado",
        EstadoAviso.Fallido => "Falló",
        _ => "Pendiente"
    };

    public static string Porcentaje(double valor) => valor.ToString("0.0") + " %";

    public static string Importe(decimal valor) => "$ " + valor.ToString("N2");

    public static string Corto(Guid id) => id.ToString("N")[..8];

    public static string Fecha(DateTime? utc) =>
        utc is { } f ? f.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss") : "—";
}
