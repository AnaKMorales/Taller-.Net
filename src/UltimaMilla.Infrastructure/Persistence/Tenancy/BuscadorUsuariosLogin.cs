using Microsoft.AspNetCore.Identity;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

public sealed class BuscadorUsuariosLogin(DbContextOptions<AppDbContext> options) : IBuscadorUsuariosLogin
{
    // Hash de una contraseña cualquiera: se verifica contra él cuando el email no existe, para que
    // la respuesta tarde lo mismo y no delate qué emails están registrados.
    private static readonly string HashFicticio = ContrasenasUsuario.Hasher.HashPassword(null!, Guid.NewGuid().ToString());

    public async Task<UsuarioLogin?> ValidarAsync(string email, string contrasena, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(contrasena)) return null;
        var normalizado = email.Trim().ToLowerInvariant();
        await using var db = new AppDbContext(options);
        // Igual que SeguimientoPublico con el token: único acceso sin contexto de operador,
        // con búsqueda exacta, y solo devuelve lo necesario para armar los claims.
        var encontrados = await db.Set<Usuario>().IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Activo && EF.Property<string>(u, "EmailNormalizado") == normalizado)
            .Select(u => new
            {
                u.Id,
                OperadorId = u.Operador.Id,
                u.Email,
                u.Rol,
                Hash = EF.Property<string?>(u, ContrasenasUsuario.Columna),
                Cuentas = u.CuentasComerciales.Select(c => c.Id).Take(2).ToList()
            })
            .Take(2)
            .ToListAsync(cancellationToken);

        // El email es único por operador, no en toda la plataforma: si se repite, no se adivina cuál es.
        var usuario = encontrados.Count == 1 ? encontrados[0] : null;
        var resultado = ContrasenasUsuario.Hasher.VerifyHashedPassword(null!, usuario?.Hash ?? HashFicticio, contrasena);
        if (usuario?.Hash is null || resultado == PasswordVerificationResult.Failed) return null;

        return new UsuarioLogin(usuario.Id, usuario.OperadorId, usuario.Email, usuario.Rol,
            usuario.Cuentas.Count == 1 ? usuario.Cuentas[0] : null);
    }
}
