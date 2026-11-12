using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Backoffice;

// Detalle de un envío para el backoffice del operador: estado por bulto, intentos con el resultado de
// cada bulto, eventos y la configuración con la que se admitió. Es para usuarios internos del operador,
// así que incluye datos del destinatario (a diferencia del seguimiento público).
public sealed record DetalleEnvioDto(
    Guid Id,
    string CodigoSeguimiento,
    EstadoEnvio Estado,
    ModalidadServicio Modalidad,
    string Comercio,
    string Destinatario,
    string Telefono,
    string Direccion,
    string Zona,
    DateTimeOffset VentanaDesde,
    DateTimeOffset VentanaHasta,
    DateTime FechaComprometida,
    decimal Tarifa,
    int VersionReglas,
    int VersionTarifario,
    bool PermiteEntregaParcial,
    AccionBultosPendientes AccionPendientes,
    string? RepartidorAsignado,
    EstadoDevolucion? Devolucion,
    IReadOnlyList<BultoDetalleDto> Bultos,
    IReadOnlyList<IntentoDetalleDto> Intentos,
    IReadOnlyList<EventoDetalleDto> Eventos,
    string? SinNuevaVisita = null)
{
    // Orientación para la pantalla: null si una nueva visita es posible; si no, el motivo. El núcleo
    // (Envio.Reprogramar) vuelve a validar al asignar, así que esto solo evita ofrecer algo que se va a rechazar.
    public bool NuevaVisitaPosible => SinNuevaVisita is null;

    // Bultos que todavía no se entregaron ni se devolvieron: los que puede gestionar el despachador.
    public IEnumerable<BultoDetalleDto> Pendientes =>
        Bultos.Where(b => b.Estado is EstadoBulto.NoEntregado or EstadoBulto.Reprogramado);

    // Mismas condiciones que exige IDespachoPendientes: entrega parcial, con pendientes y sin otra visita
    // ni devolución en curso. Así la pantalla solo ofrece acciones que el núcleo va a aceptar.
    public bool AdmiteGestionDePendientes =>
        Estado == EstadoEnvio.EntregadoParcialmente && RepartidorAsignado is null && Devolucion is null && Pendientes.Any();
}

public sealed record BultoDetalleDto(string Codigo, decimal PesoKg, decimal VolumenM3, EstadoBulto Estado);

public sealed record IntentoDetalleDto(int Numero, DateTime OcurridoEn, ResultadoIntento Resultado, string? Motivo,
    IReadOnlyList<ResultadoBultoDetalleDto> Bultos, IReadOnlyList<TipoPrueba> Pruebas);

public sealed record ResultadoBultoDetalleDto(string Codigo, ResultadoIntento Resultado, string? Motivo, int Evidencias);

public sealed record EventoDetalleDto(DateTime OcurridoEn, TipoEvento Tipo, OrigenEvento Origen,
    EstadoEnvio? EstadoAnterior, EstadoEnvio EstadoNuevo, string Responsable, string? Detalle);

// Solo encuentra envíos del operador del usuario; null si no existe o es de otro operador.
public interface IDetalleEnvio
{
    Task<DetalleEnvioDto?> ObtenerAsync(Guid envioId, CancellationToken cancellationToken = default);
}
