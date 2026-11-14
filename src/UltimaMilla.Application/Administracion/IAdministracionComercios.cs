namespace UltimaMilla.Application.Administracion;

public sealed record CrearComercio(string RazonSocial, string Telefono, string Email);
public sealed record CrearUsuarioComercio(string Email, string Contrasena);
public sealed record CuentaAdministracionDto(Guid CuentaId, Guid ComercioId, string RazonSocial, string Telefono, string Email);
public sealed record UsuarioComercioAdministracionDto(Guid Id, string Email, bool Activo);

public interface IAdministracionComercios
{
    Task<IReadOnlyList<CuentaAdministracionDto>> CuentasAsync(CancellationToken ct = default);
    Task<CuentaAdministracionDto> CrearAsync(CrearComercio solicitud, CancellationToken ct = default);
    Task<IReadOnlyList<UsuarioComercioAdministracionDto>> UsuariosAsync(Guid cuentaId, CancellationToken ct = default);
    Task<UsuarioComercioAdministracionDto> CrearUsuarioAsync(Guid cuentaId, CrearUsuarioComercio solicitud, CancellationToken ct = default);
}
