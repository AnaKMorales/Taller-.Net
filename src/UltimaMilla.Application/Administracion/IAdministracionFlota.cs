namespace UltimaMilla.Application.Administracion;

public sealed record CrearRepartidor(string Email, string Contrasena);
public sealed record CrearVehiculo(string Matricula, decimal CapacidadPesoKg, decimal CapacidadVolumenM3);
public sealed record RepartidorAdministracionDto(Guid Id, string Email, bool Activo);
public sealed record VehiculoAdministracionDto(Guid Id, string Matricula, decimal CapacidadPesoKg, decimal CapacidadVolumenM3);

public interface IAdministracionFlota
{
    Task<IReadOnlyList<RepartidorAdministracionDto>> RepartidoresAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VehiculoAdministracionDto>> VehiculosAsync(CancellationToken ct = default);
    Task<RepartidorAdministracionDto> CrearRepartidorAsync(CrearRepartidor solicitud, CancellationToken ct = default);
    Task<VehiculoAdministracionDto> CrearVehiculoAsync(CrearVehiculo solicitud, CancellationToken ct = default);
}
