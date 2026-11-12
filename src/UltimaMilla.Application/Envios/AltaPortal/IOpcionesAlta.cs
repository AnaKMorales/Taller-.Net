using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.AltaPortal;

// Lo que un comercio puede elegir al dar de alta un envío con su operador: las zonas con sus franjas y las
// modalidades que ofrecen las reglas vigentes. Todo sale del contexto autenticado, nunca del formulario.
public sealed record OpcionesAltaDto(Guid OperadorId, Guid CuentaComercialId, Guid ReglasId,
    IReadOnlyList<ZonaAltaDto> Zonas, IReadOnlyList<PlazoAltaDto> Plazos);

public sealed record ZonaAltaDto(Guid Id, string Nombre, string CodigoPostal, IReadOnlyList<FranjaAltaDto> Franjas);

public sealed record FranjaAltaDto(Guid Id, string Nombre, TimeOnly Desde, TimeOnly Hasta);

public sealed record PlazoAltaDto(ModalidadServicio Modalidad, int Horas, TimeOnly HoraCorte);

public interface IOpcionesAlta
{
    // Lanza AislamientoOperadorException si el usuario no es un comercio con cuenta activa.
    // Devuelve null si el operador todavía no tiene reglas vigentes.
    Task<OpcionesAltaDto?> ObtenerAsync(CancellationToken cancellationToken = default);
}
