using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Administracion;
using UltimaMilla.Web.Pages.Backoffice.Deposito;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages.Backoffice.Administracion;

// Ficha de la cuenta comercial y sus usuarios del portal.
[Authorize(Policy = Politicas.Administracion)]
public sealed class ComercioModel(IAdministracionComercios comercios) : OperacionDepositoModel
{
    [BindProperty(SupportsGet = true)] public Guid Id { get; set; }
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public string? RazonSocial { get; set; }
    [BindProperty] public string? Telefono { get; set; }
    [BindProperty] public string? Email { get; set; }
    [BindProperty] public string? Contrasena { get; set; }
    [BindProperty] public Guid UsuarioId { get; set; }
    [BindProperty] public string? EmailActual { get; set; }

    public CuentaAdministracionDto? Cuenta { get; private set; }
    public IReadOnlyList<UsuarioComercioAdministracionDto> Usuarios { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await CargarAsync(ct);
        return Cuenta is null ? NotFound() : Page();
    }

    public Task<IActionResult> OnPostModificarAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        await comercios.ModificarCuentaAsync(Id, new ModificarCuentaComercial(RazonSocial ?? "", Telefono ?? "", Email ?? "", Version), ct);
        return "Datos del comercio actualizados.";
    }, ct);

    public Task<IActionResult> OnPostBajaAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        await comercios.DarBajaCuentaAsync(Id, new BajaCuentaComercial(Version), ct);
        return "Comercio dado de baja. Sus usuarios ya no pueden ingresar ni crear envíos; el historial se conserva.";
    }, ct);

    public Task<IActionResult> OnPostUsuarioAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var u = await comercios.CrearUsuarioAsync(Id, new CrearUsuarioComercio(Email ?? "", Contrasena ?? ""), ct);
        return $"Usuario {u.Email} creado.";
    }, ct);

    // El email actual viaja como versión esperada: si otro lo cambió, se rechaza
    public Task<IActionResult> OnPostCambiarEmailAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var u = await comercios.ModificarUsuarioAsync(Id, UsuarioId,
            new ModificarUsuarioComercio(Email ?? "", new EstadoUsuarioEsperado(EmailActual ?? "", true)), ct);
        return $"Email cambiado a {u.Email}.";
    }, ct);

    public Task<IActionResult> OnPostBajaUsuarioAsync(CancellationToken ct) => EjecutarAsync(async () =>
    {
        var u = await comercios.DarBajaUsuarioAsync(Id, UsuarioId, new EstadoUsuarioEsperado(EmailActual ?? "", true), ct);
        return $"Usuario {u.Email} dado de baja.";
    }, ct);

    protected override async Task CargarAsync(CancellationToken ct)
    {
        Cuenta = (await comercios.CuentasAsync(ct)).SingleOrDefault(c => c.CuentaId == Id);
        if (Cuenta is null) return;
        try { Usuarios = await comercios.UsuariosAsync(Id, ct); }
        catch (AislamientoOperadorException) { Usuarios = []; }
    }
}
