using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Tenancy;

public sealed record UsuarioLogin(Guid UsuarioId, Guid OperadorId, string Email, RolUsuario Rol, Guid? CuentaComercialId);

// Valida el ingreso antes de conocer el operador del usuario, que es lo que hace cualquier login.
public interface IBuscadorUsuariosLogin
{
    // Devuelve null si el email no existe, el usuario está inactivo, la contraseña no coincide
    // o el email se repite en más de un operador. No dice cuál de esos fue el motivo.
    Task<UsuarioLogin?> ValidarAsync(string email, string contrasena, CancellationToken cancellationToken = default);
}
