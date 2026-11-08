using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tenancy;

// Vista pública del envío para el destinatario (sin login).
public sealed record SeguimientoPublicoDto(
    string Operador,
    EstadoEnvio Estado,
    DateTime FechaComprometida,
    DateTimeOffset VentanaDesde,
    DateTimeOffset VentanaHasta,
    AvanceBultosDto Bultos,
    IReadOnlyList<HitoSeguimientoDto> Historial)
{
    // El historial es una lista
    public bool Equals(SeguimientoPublicoDto? otro) =>
        otro is not null && Operador == otro.Operador && Estado == otro.Estado &&
        FechaComprometida == otro.FechaComprometida && VentanaDesde == otro.VentanaDesde &&
        VentanaHasta == otro.VentanaHasta && Bultos == otro.Bultos && Historial.SequenceEqual(otro.Historial);

    public override int GetHashCode() => HashCode.Combine(Operador, Estado, FechaComprometida, Bultos, Historial.Count);
}

// Avance de la entrega parcial: por ejemplo, "2 de 3 bultos entregados".
public sealed record AvanceBultosDto(int Total, int Entregados, int EnDevolucion);

public sealed record HitoSeguimientoDto(TipoEvento Tipo, EstadoEnvio Estado, DateTime OcurridoEn);

public interface ISeguimientoPublico
{
    Task<SeguimientoPublicoDto?> ConsultarAsync(string token, CancellationToken cancellationToken = default);
}
