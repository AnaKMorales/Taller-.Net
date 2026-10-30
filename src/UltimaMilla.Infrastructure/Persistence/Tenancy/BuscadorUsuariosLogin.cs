using Microsoft.AspNetCore.Identity;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

public sealed class BuscadorUsuariosLogin(DbContextOptions<AppDbContext> options) : IBuscadorUsuariosLogin
{
    // Para que el tiempo de respuesta no revele si el email existe.
    private static readonly string HashFicticio = ContrasenasUsuario.Hasher.HashPassword(null!, Guid.NewGuid().ToString());

    public async Task<UsuarioLogin?> ValidarAsync(string email, string contrasena, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(contrasena)) return null;
        var normalizado = email.Trim().ToLowerInvariant();
        await using var db = new AppDbContext(options);
        // Sin filtro de operador: todavía no se sabe a cuál pertenece el usuario.
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

        // El email es único por operador: si se repite, no se elige uno.
        var usuario = encontrados.Count == 1 ? encontrados[0] : null;
        var resultado = ContrasenasUsuario.Hasher.VerifyHashedPassword(null!, usuario?.Hash ?? HashFicticio, contrasena);
        if (usuario?.Hash is null || resultado == PasswordVerificationResult.Failed) return null;

        return new UsuarioLogin(usuario.Id, usuario.OperadorId, usuario.Email, usuario.Rol,
            usuario.Cuentas.Count == 1 ? usuario.Cuentas[0] : null);
    }
}
