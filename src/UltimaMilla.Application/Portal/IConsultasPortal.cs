using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Portal;

public sealed record EnvioPortalDto(Guid EnvioId, string CodigoSeguimiento, string Destinatario, EstadoEnvio Estado,
    DateTime FechaAdmision);

public sealed record BultoEtiquetaDto(string Codigo, decimal Peso);

public sealed record EtiquetaEnvioDto(string CodigoSeguimiento, string Operador, string Comercio,
    string Destinatario, string Telefono, string Calle, string Numero, string Localidad, string CodigoPostal,
    string Zona, ModalidadServicio Modalidad, DateTime FechaComprometida, IReadOnlyList<BultoEtiquetaDto> Bultos);

// Solo envíos de la cuenta comercial del usuario.
public interface IConsultasPortal
{
    Task<IReadOnlyList<EnvioPortalDto>> EnviosAsync(CancellationToken cancellationToken = default);

    // null si no existe o es de otro comercio
    Task<EtiquetaEnvioDto?> EtiquetaAsync(string codigoSeguimiento, CancellationToken cancellationToken = default);
}
