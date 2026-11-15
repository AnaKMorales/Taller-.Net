namespace UltimaMilla.Application.Administracion;

public sealed record CrearComercio(string RazonSocial, string Telefono, string Email);
public sealed record CrearUsuarioComercio(string Email, string Contrasena);
public sealed record EstadoUsuarioEsperado(string Email, bool? Activo);
public sealed record ModificarUsuarioComercio(string Email, EstadoUsuarioEsperado Esperado);
public sealed record ModificarCuentaComercial(string RazonSocial, string Telefono, string Email, Guid VersionEsperada);
public sealed record BajaCuentaComercial(Guid VersionEsperada);
public sealed record CuentaAdministracionDto(Guid CuentaId, Guid ComercioId, string RazonSocial, string Telefono, string Email,
    bool Activa = true, Guid Version = default);
public sealed record UsuarioComercioAdministracionDto(Guid Id, string Email, bool Activo);

public interface IAdministracionComercios
{
    Task<IReadOnlyList<CuentaAdministracionDto>> CuentasAsync(CancellationToken ct = default);
    Task<CuentaAdministracionDto> CrearAsync(CrearComercio solicitud, CancellationToken ct = default);
    Task<CuentaAdministracionDto> ModificarCuentaAsync(Guid cuentaId, ModificarCuentaComercial solicitud, CancellationToken ct = default);
    Task<CuentaAdministracionDto> DarBajaCuentaAsync(Guid cuentaId, BajaCuentaComercial solicitud, CancellationToken ct = default);
    Task<IReadOnlyList<UsuarioComercioAdministracionDto>> UsuariosAsync(Guid cuentaId, CancellationToken ct = default);
    Task<UsuarioComercioAdministracionDto> CrearUsuarioAsync(Guid cuentaId, CrearUsuarioComercio solicitud, CancellationToken ct = default);
    Task<UsuarioComercioAdministracionDto> ModificarUsuarioAsync(Guid cuentaId, Guid usuarioId, ModificarUsuarioComercio solicitud, CancellationToken ct = default);
    Task<UsuarioComercioAdministracionDto> DarBajaUsuarioAsync(Guid cuentaId, Guid usuarioId, EstadoUsuarioEsperado esperado, CancellationToken ct = default);
}
