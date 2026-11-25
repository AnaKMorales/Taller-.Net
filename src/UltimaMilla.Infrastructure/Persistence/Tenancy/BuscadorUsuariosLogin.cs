using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Identidad;

namespace UltimaMilla.Infrastructure.Persistence.Tenancy;

// Login con ASP.NET Identity: UserManager sobre el store de EF Core (AspNetUsers). Hace lo mismo que
// SignInManager.CheckPasswordSignInAsync (bloqueo, intentos fallidos) sin emitir la sesión: la cookie o el Bearer
// los emite Web con los claims de siempre.
public sealed class BuscadorUsuariosLogin(DbContextOptions<AppDbContext> options, FabricaCuentasAcceso? fabrica = null) : IBuscadorUsuariosLogin
{
    private readonly FabricaCuentasAcceso _fabrica = fabrica ?? FabricaCuentasAcceso.Predeterminada;

    public async Task<UsuarioLogin?> ValidarAsync(string email, string contrasena, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(contrasena)) return null;
        // Sin tenant: todavía no se sabe a qué operador pertenece el usuario. Solo puede guardar el registro del login.
        await using var db = new AppDbContext(options);
        using var usuarios = _fabrica.Para(db);
        var cuenta = await usuarios.FindByNameAsync(email.Trim());
        var usuario = cuenta is null ? null : await db.Set<Usuario>().IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == cuenta.Id && u.Activo && (u.Rol != RolUsuario.Comercio || u.CuentasComerciales.Any(c => c.Activa)))
            .Select(u => new
            {
                u.Id,
                OperadorId = u.Operador.Id,
                u.Email,
                u.Rol,
                Cuentas = u.CuentasComerciales.Where(c => c.Activa).Select(c => c.Id).Take(2).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Email inexistente, usuario inactivo o cuenta bloqueada: misma respuesta y un costo de hash parecido.
        if (cuenta is null || usuario is null || await usuarios.IsLockedOutAsync(cuenta))
        {
            _fabrica.VerificarFicticio(contrasena);
            return null;
        }

        try
        {
            if (!await usuarios.CheckPasswordAsync(cuenta, contrasena))
            {
                // Al fallo 20 Identity fija LockoutEnd (5 minutos) y reinicia el contador. Es el respaldo: el bloqueo
                // de 5 fallos lo hace Web por (correo, IP), para que conocer el correo no alcance para dejar a alguien afuera.
                await usuarios.AccessFailedAsync(cuenta);
                return null;
            }
            await usuarios.ResetAccessFailedCountAsync(cuenta);
        }
        catch (ConflictoConcurrenciaException)
        {
            // Otro login del mismo usuario guardó antes (ConcurrencyStamp): se rechaza este intento sin dar detalles.
            return null;
        }

        return new UsuarioLogin(usuario.Id, usuario.OperadorId, usuario.Email, usuario.Rol,
            usuario.Cuentas.Count == 1 ? usuario.Cuentas[0] : null);
    }
}
