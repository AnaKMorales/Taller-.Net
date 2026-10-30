using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Tenancy;

public sealed record UsuarioLogin(Guid UsuarioId, Guid OperadorId, string Email, RolUsuario Rol, Guid? CuentaComercialId);

public interface IBuscadorUsuariosLogin
{
    // null si el usuario no existe, está inactivo o la contraseña no coincide.
    Task<UsuarioLogin?> ValidarAsync(string email, string contrasena, CancellationToken cancellationToken = default);
}
