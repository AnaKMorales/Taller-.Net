using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UltimaMilla.Infrastructure.Persistence.Identidad;

// Arma un UserManager de ASP.NET Identity sobre un AppDbContext dado, con el store de EF Core (UserOnlyStore).
// Se recibe el contexto en vez de usar el del contenedor porque cada caso de uso abre el suyo con su tenant:
// así el alta del Usuario y la de su CuentaAcceso van en el mismo SaveChanges (una sola transacción).
public sealed class FabricaCuentasAcceso
{
    public const string MensajeContrasena =
        "La contraseña debe tener al menos 8 caracteres, con mayúscula, minúscula, número y símbolo.";
    // Genérico a propósito: no revela si el email existe en otro operador.
    public const string MensajeEmailNoDisponible = "No se pudo crear el usuario con ese email.";
    // Índice único de Identity sobre NormalizedUserName (AspNetUsers).
    public const string IndiceUsuarioUnico = "UserNameIndex";

    public static FabricaCuentasAcceso Predeterminada { get; } = Crear();

    private readonly IServiceProvider _servicios;
    private readonly IOptions<IdentityOptions> _opciones;
    private readonly IPasswordHasher<CuentaAcceso> _hasher;
    private readonly IUserValidator<CuentaAcceso>[] _validadoresUsuario;
    private readonly IPasswordValidator<CuentaAcceso>[] _validadoresContrasena;
    private readonly ILookupNormalizer _normalizador;
    private readonly IdentityErrorDescriber _errores;
    private readonly ILogger<UserManager<CuentaAcceso>> _log;
    // Para que el tiempo de respuesta del login no revele si el email existe.
    private readonly string _hashFicticio;

    private FabricaCuentasAcceso(IServiceProvider servicios)
    {
        _servicios = servicios;
        _opciones = servicios.GetRequiredService<IOptions<IdentityOptions>>();
        _hasher = servicios.GetRequiredService<IPasswordHasher<CuentaAcceso>>();
        _validadoresUsuario = [.. servicios.GetServices<IUserValidator<CuentaAcceso>>()];
        _validadoresContrasena = [.. servicios.GetServices<IPasswordValidator<CuentaAcceso>>()];
        _normalizador = servicios.GetRequiredService<ILookupNormalizer>();
        _errores = servicios.GetRequiredService<IdentityErrorDescriber>();
        _log = servicios.GetRequiredService<ILogger<UserManager<CuentaAcceso>>>();
        _hashFicticio = _hasher.HashPassword(new CuentaAcceso(), Guid.NewGuid().ToString());
    }

    public static void Configurar(IdentityOptions o)
    {
        o.Password.RequiredLength = 8;
        o.Password.RequireUppercase = true;
        o.Password.RequireLowercase = true;
        o.Password.RequireDigit = true;
        o.Password.RequireNonAlphanumeric = true;
        o.Password.RequiredUniqueChars = 1;
        o.Lockout.AllowedForNewUsers = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        // UserName = email; el formato del email lo validan los casos de uso.
        o.User.AllowedUserNameCharacters = "";
        o.User.RequireUniqueEmail = true;
    }

    private static FabricaCuentasAcceso Crear()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddIdentityCore<CuentaAcceso>(Configurar);
        return new FabricaCuentasAcceso(servicios.BuildServiceProvider());
    }

    // guardarAlInstante = false: el store solo deja los cambios en el contexto y los guarda el SaveChanges del caso de uso.
    // Ojo: en ese modo se hace UNA sola actualización por cuenta y contexto; una segunda llamada del store (Attach)
    // tomaría el ConcurrencyStamp sin guardar como original y el UPDATE no encontraría la fila.
    public UserManager<CuentaAcceso> Para(AppDbContext db, bool guardarAlInstante = true) =>
        Para(db, guardarAlInstante, _validadoresContrasena);

    private UserManager<CuentaAcceso> Para(AppDbContext db, bool guardarAlInstante, IPasswordValidator<CuentaAcceso>[] validadores)
    {
        ArgumentNullException.ThrowIfNull(db);
        var store = new UserOnlyStore<CuentaAcceso, AppDbContext, Guid>(db, _errores) { AutoSaveChanges = guardarAlInstante };
        return new UserManager<CuentaAcceso>(store, _opciones, _hasher, _validadoresUsuario, validadores,
            _normalizador, _errores, _servicios, _log);
    }

    internal void VerificarFicticio(string contrasena) =>
        _hasher.VerifyHashedPassword(new CuentaAcceso(), _hashFicticio, contrasena);

    // Crea la CuentaAcceso del usuario con UserManager.CreateAsync; la guarda el próximo SaveChanges del contexto.
    public Task CrearAsync(AppDbContext db, Usuario usuario, string contrasena) =>
        CrearAsync(db, usuario, contrasena, _validadoresContrasena);

    // Solo para POST /dev/datos-demo con clave propia: esa clave (aleatoria, la genera deploy/aws/sembrar-demo.sh) se
    // sigue validando como antes, por largo mínimo y sin exigir símbolos. La cuenta igual se crea con UserManager.
    public Task CrearConClaveDemoAsync(AppDbContext db, Usuario usuario, string contrasena, int largoMinimo) =>
        CrearAsync(db, usuario, contrasena, [new ValidadorLargoMinimo(largoMinimo)]);

    private async Task CrearAsync(AppDbContext db, Usuario usuario, string contrasena, IPasswordValidator<CuentaAcceso>[] validadores)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        using var usuarios = Para(db, guardarAlInstante: false, validadores);
        var cuenta = new CuentaAcceso { Id = usuario.Id, UserName = usuario.Email, Email = usuario.Email };
        Verificar(await usuarios.CreateAsync(cuenta, contrasena ?? ""));
    }

    // Cambia usuario y email de la CuentaAcceso (una sola actualización); lo guarda el próximo SaveChanges.
    // Un usuario sin cuenta de acceso no puede ingresar: no hay credencial que sincronizar.
    public async Task CambiarEmailAsync(AppDbContext db, Guid usuarioId, string email)
    {
        using var usuarios = Para(db, guardarAlInstante: false);
        if (await usuarios.FindByIdAsync(usuarioId.ToString()) is not { } cuenta) return;
        cuenta.UserName = email;
        cuenta.Email = email;
        cuenta.EmailConfirmed = false;
        // Lo mismo que hacen SetUserNameAsync/SetEmailAsync: invalida lo emitido con el email anterior.
        cuenta.SecurityStamp = Guid.NewGuid().ToString("N").ToUpperInvariant();
        // UpdateAsync valida (usuario y email únicos), normaliza y deja el UPDATE pendiente.
        Verificar(await usuarios.UpdateAsync(cuenta));
    }

    private sealed class ValidadorLargoMinimo(int largo) : IPasswordValidator<CuentaAcceso>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<CuentaAcceso> manager, CuentaAcceso user, string? password) =>
            Task.FromResult((password?.Length ?? 0) >= largo ? IdentityResult.Success
                : IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort", Description = $"Mínimo {largo} caracteres." }));
    }

    private static void Verificar(IdentityResult resultado)
    {
        if (resultado.Succeeded) return;
        if (resultado.Errors.Any(e => e.Code.StartsWith("Password", StringComparison.Ordinal)))
            throw new ArgumentException(MensajeContrasena);
        throw new InvalidOperationException(MensajeEmailNoDisponible);
    }
}
