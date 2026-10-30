using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Pages;

// Un solo login para todos los roles; después de ingresar, cada uno va a su aplicación.
// Emite la misma cookie y los mismos claims que emitirá ASP.NET Identity cuando se integre.
[AllowAnonymous]
public sealed class LoginModel(IBuscadorUsuariosLogin usuarios, IWebHostEnvironment entorno) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = "";

    [BindProperty]
    public string Contrasena { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public bool Desarrollo => entorno.IsDevelopment();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var usuario = await usuarios.ValidarAsync(Email, Contrasena, cancellationToken);
        if (usuario is null)
        {
            // Mismo mensaje para email inexistente y contraseña incorrecta: no se revela qué emails existen.
            Error = "Email o contraseña incorrectos.";
            return Page();
        }

        if (Politicas.Inicio(usuario.Rol) is not { } inicio)
        {
            Error = "Los repartidores ingresan desde la aplicación del repartidor.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.UsuarioId.ToString()),
            new(ClaimTypes.Name, usuario.Email),
            new(ClaimTypes.Role, usuario.Rol.ToString()),
            new(ClaimsUltimaMilla.Operador, usuario.OperadorId.ToString())
        };
        if (usuario.CuentaComercialId is { } cuenta) claims.Add(new(ClaimsUltimaMilla.CuentaComercial, cuenta.ToString()));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : inicio);
    }

    public async Task<IActionResult> OnPostSalirAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage();
    }
}
