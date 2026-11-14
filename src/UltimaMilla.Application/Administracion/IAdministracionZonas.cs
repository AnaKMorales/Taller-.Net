namespace UltimaMilla.Application.Administracion;

public sealed record CrearFranjaHoraria(string Nombre, TimeOnly Desde, TimeOnly Hasta);
public sealed record CrearZonaCobertura(string Nombre, string CodigoPostal, IReadOnlyList<CrearFranjaHoraria> Franjas);
public sealed record FranjaAdministracionDto(Guid Id, string Nombre, TimeOnly Desde, TimeOnly Hasta);
public sealed record ZonaAdministracionDto(Guid Id, string Nombre, string CodigoPostal, IReadOnlyList<FranjaAdministracionDto> Franjas);

public interface IAdministracionZonas
{
    Task<IReadOnlyList<ZonaAdministracionDto>> ZonasAsync(CancellationToken ct = default);
    Task<ZonaAdministracionDto> CrearZonaAsync(CrearZonaCobertura solicitud, CancellationToken ct = default);
    Task<IReadOnlyList<FranjaAdministracionDto>> FranjasAsync(Guid zonaId, CancellationToken ct = default);
    Task<FranjaAdministracionDto> CrearFranjaAsync(Guid zonaId, CrearFranjaHoraria solicitud, CancellationToken ct = default);
}
