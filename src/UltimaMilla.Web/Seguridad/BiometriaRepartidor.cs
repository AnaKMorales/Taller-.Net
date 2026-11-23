using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Usuarios;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Identidad;
using UltimaMilla.Web.Api;

namespace UltimaMilla.Web.Seguridad;

public static class BiometriaRepartidorEndpoints
{
    public static void AgregarBiometriaRepartidor(this IServiceCollection servicios)
    {
        servicios.Configure<IdentityPasskeyOptions>(opciones =>
        {
            opciones.AuthenticatorAttachment = "platform";
            opciones.UserVerificationRequirement = "required";
            opciones.AuthenticatorTimeout = DesafiosBiometria.Vigencia;
        });
        servicios.AddSingleton<DesafiosBiometria>();
        servicios.AddScoped<BiometriaRepartidor>();
    }

    public static void MapearBiometriaRepartidor(this RouteGroupBuilder api)
    {
        var grupo = api.MapGroup("/biometria").RequireAuthorization(Politicas.Repartidor)
            .AddEndpointFilter<ErroresApiFilter>()
            .AddEndpointFilter(async (invocacion, siguiente) =>
            {
                invocacion.HttpContext.Response.Headers.CacheControl = "no-store";
                return await siguiente(invocacion);
            });

        grupo.MapGet("/estado", (BiometriaRepartidor biometria) => biometria.EstadoAsync());
        grupo.MapPost("/registro/opciones", (SolicitudRegistroBiometria solicitud, BiometriaRepartidor biometria) =>
            biometria.OpcionesRegistroAsync(solicitud.Contrasena));
        grupo.MapPost("/registro", (RespuestaBiometria solicitud, BiometriaRepartidor biometria) =>
            biometria.RegistrarAsync(solicitud));
        grupo.MapPost("/contrasena/opciones", (BiometriaRepartidor biometria) => biometria.OpcionesCambioAsync());
        grupo.MapPost("/contrasena", (SolicitudCambioBiometrico solicitud, BiometriaRepartidor biometria) =>
            biometria.CambiarContrasenaAsync(solicitud));
    }
}

internal sealed record SolicitudRegistroBiometria(string? Contrasena);
internal sealed record RespuestaBiometria(Guid DesafioId, JsonElement Credencial);
internal sealed record SolicitudCambioBiometrico(Guid DesafioId, JsonElement Credencial, string? Actual, string? Nueva);

internal sealed class BiometriaRepartidor(AppDbContext db, ITenantActual tenant, IHttpContextAccessor http,
    IBuscadorUsuariosLogin login, IOptions<IdentityPasskeyOptions> opciones, DesafiosBiometria desafios)
{
    private HttpContext Http => http.HttpContext!;
    private CancellationToken Ct => Http.RequestAborted;

    public async Task<IResult> EstadoAsync()
    {
        if (await CuentaAsync() is not { } cuenta) return Results.Unauthorized();
        using var usuarios = FabricaCuentasAcceso.Predeterminada.Para(db);
        return Results.Ok(new { configurada = (await usuarios.GetPasskeysAsync(cuenta)).Count > 0 });
    }

    public async Task<IResult> OpcionesRegistroAsync(string? contrasena)
    {
        if (await CuentaAsync() is not { } cuenta) return Results.Unauthorized();
        if (!ContrasenaValida(contrasena)) return Error("Ingresá tu contraseña actual.");
        var verificado = await login.ValidarAsync(cuenta.Email!, contrasena!, Ct);
        if (verificado?.UsuarioId != cuenta.Id || verificado.OperadorId != tenant.Contexto!.OperadorId)
            return Error("No pudimos verificar tu contraseña.");

        using var usuarios = FabricaCuentasAcceso.Predeterminada.Para(db);
        if ((await usuarios.GetPasskeysAsync(cuenta)).Count != 0) return Error("Tu cuenta ya tiene una passkey registrada.");
        var resultado = await new PasskeyHandler<CuentaAcceso>(usuarios, opciones).MakeCreationOptionsAsync(
            new PasskeyUserEntity { Id = cuenta.Id.ToString(), Name = cuenta.Email!, DisplayName = cuenta.Email! }, Http);
        return DarOpciones(cuenta, OperacionBiometrica.Registrar, resultado.AttestationState, resultado.CreationOptionsJson);
    }

    public async Task<IResult> RegistrarAsync(RespuestaBiometria solicitud)
    {
        if (await CuentaAsync() is not { } cuenta) return Results.Unauthorized();
        var estado = Consumir(cuenta, OperacionBiometrica.Registrar, solicitud.DesafioId, solicitud.Credencial);
        if (estado is null) return Error("La solicitud de registro venció o ya fue utilizada.");

        using var usuarios = FabricaCuentasAcceso.Predeterminada.Para(db);
        if ((await usuarios.GetPasskeysAsync(cuenta)).Count != 0) return Error("Tu cuenta ya tiene una passkey registrada.");
        var resultado = await new PasskeyHandler<CuentaAcceso>(usuarios, opciones).PerformAttestationAsync(new()
        {
            HttpContext = Http,
            AttestationState = estado,
            CredentialJson = solicitud.Credencial.GetRawText()
        });
        if (!resultado.Succeeded || resultado.UserEntity.Id != cuenta.Id.ToString())
            return Error("El dispositivo no pudo verificar el registro.");
        try
        {
            var guardada = await usuarios.AddOrUpdatePasskeyAsync(cuenta, resultado.Passkey);
            return guardada.Succeeded ? Results.NoContent() : Error("No se pudo guardar la passkey.");
        }
        catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException
            { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_AspNetUserPasskeys_UserId" or "PK_AspNetUserPasskeys" })
        {
            return Error("La passkey ya fue registrada. Volvé a abrir Seguridad de mi cuenta.");
        }
    }

    public async Task<IResult> OpcionesCambioAsync()
    {
        if (await CuentaAsync() is not { } cuenta) return Results.Unauthorized();
        using var usuarios = FabricaCuentasAcceso.Predeterminada.Para(db);
        if ((await usuarios.GetPasskeysAsync(cuenta)).Count == 0) return Error("Configurá una passkey primero.");
        var resultado = await new PasskeyHandler<CuentaAcceso>(usuarios, opciones).MakeRequestOptionsAsync(cuenta, Http);
        return DarOpciones(cuenta, OperacionBiometrica.CambiarContrasena, resultado.AssertionState, resultado.RequestOptionsJson);
    }

    public async Task<IResult> CambiarContrasenaAsync(SolicitudCambioBiometrico solicitud)
    {
        if (await CuentaAsync() is not { } cuenta) return Results.Unauthorized();
        var estado = Consumir(cuenta, OperacionBiometrica.CambiarContrasena, solicitud.DesafioId, solicitud.Credencial);
        if (estado is null) return Error("La solicitud de cambio venció o ya fue utilizada.");
        if (!ContrasenaValida(solicitud.Actual) || !ContrasenaValida(solicitud.Nueva))
            return Error("Ingresá la contraseña actual y una nueva válida.");

        using var usuarios = FabricaCuentasAcceso.Predeterminada.Para(db);
        var resultado = await new PasskeyHandler<CuentaAcceso>(usuarios, opciones).PerformAssertionAsync(new()
        {
            HttpContext = Http,
            AssertionState = estado,
            CredentialJson = solicitud.Credencial.GetRawText()
        });
        if (!resultado.Succeeded || resultado.User.Id != cuenta.Id)
            return Error("No pudimos verificar la confirmación del dispositivo.");

        await using var transaccion = await db.Database.BeginTransactionAsync(Ct);
        var guardada = await usuarios.AddOrUpdatePasskeyAsync(cuenta, resultado.Passkey);
        if (!guardada.Succeeded) return Error("No se pudo actualizar la passkey.");
        var cambio = await usuarios.ChangePasswordAsync(cuenta, solicitud.Actual!, solicitud.Nueva!);
        if (!cambio.Succeeded)
            return Error(cambio.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                ? "La contraseña actual es incorrecta."
                : FabricaCuentasAcceso.MensajeContrasena);
        await transaccion.CommitAsync(Ct);
        return Results.NoContent();
    }

    private async Task<CuentaAcceso?> CuentaAsync()
    {
        // La política comprueba el token y el middleware revalida el usuario y su operador.
        if (tenant.Contexto is not { Rol: RolUsuario.Repartidor, UsuarioId: { } id }) return null;
        return await db.Set<CuentaAcceso>().SingleOrDefaultAsync(cuenta => cuenta.Id == id, Ct);
    }

    private IResult DarOpciones(CuentaAcceso cuenta, OperacionBiometrica operacion, string? estado, string opcionesJson)
    {
        if (estado is null) throw new InvalidOperationException("No se pudo preparar la verificación del dispositivo.");
        var desafioId = desafios.Crear(tenant.Contexto!.OperadorId, cuenta.Id, operacion, cuenta.SecurityStamp, estado);
        return Results.Ok(new { desafioId, opcionesJson });
    }

    private string? Consumir(CuentaAcceso cuenta, OperacionBiometrica operacion, Guid id, JsonElement credencial) =>
        credencial.ValueKind == JsonValueKind.Object && credencial.GetRawText().Length <= 32_768
            ? desafios.Consumir(tenant.Contexto!.OperadorId, cuenta.Id, operacion, cuenta.SecurityStamp, id)
            : null;

    private static bool ContrasenaValida(string? contrasena) => contrasena is { Length: > 0 and <= 256 };
    private static IResult Error(string mensaje) => Results.Problem(statusCode: 400, title: mensaje);
}

internal enum OperacionBiometrica { Registrar, CambiarContrasena }

internal sealed class DesafiosBiometria : IDisposable
{
    internal static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(2);
    private readonly MemoryCache _estados = new(new MemoryCacheOptions { SizeLimit = 4096 });
    private readonly object _candado = new();

    public Guid Crear(Guid operador, Guid usuario, OperacionBiometrica operacion, string? sello, string estado)
    {
        var id = Guid.NewGuid();
        lock (_candado)
            _estados.Set((operador, usuario, operacion), new Desafio(id, sello, estado),
                new MemoryCacheEntryOptions().SetSize(1).SetAbsoluteExpiration(Vigencia));
        return id;
    }

    public string? Consumir(Guid operador, Guid usuario, OperacionBiometrica operacion, string? sello, Guid id)
    {
        var clave = (operador, usuario, operacion);
        lock (_candado)
        {
            if (!_estados.TryGetValue<Desafio>(clave, out var desafio) || desafio is null ||
                desafio.Id != id || desafio.Sello != sello) return null;
            _estados.Remove(clave);
            return desafio.Estado;
        }
    }

    public void Dispose() => _estados.Dispose();

    private sealed record Desafio(Guid Id, string? Sello, string Estado);
}
