using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Web.Seguridad;

public static class Politicas
{
    // El backoffice es para el personal del operador; los usuarios de comercio usan el portal.
    public const string Backoffice = "Backoffice";
    public const string Portal = "Portal";

    public static readonly string[] RolesBackoffice =
        [nameof(RolUsuario.Administrador), nameof(RolUsuario.Despachador), nameof(RolUsuario.OperarioDeposito)];

    // Página de inicio de cada rol después del login. El repartidor no usa esta web: entra desde su app (PWA).
    public static string? Inicio(RolUsuario rol) => rol switch
    {
        RolUsuario.Administrador or RolUsuario.Despachador or RolUsuario.OperarioDeposito => "/backoffice/tablero",
        RolUsuario.Comercio => "/portal",
        _ => null
    };
}

// Claims que espera TenantResolver. Los arma el login; Identity tendrá que emitir los mismos.
public static class ClaimsUltimaMilla
{
    public const string Operador = "operador_id";
    public const string CuentaComercial = "cuenta_comercial_id";
}

// El contexto del operador lo resuelve un middleware por petición (Program.cs) y queda en HttpContext.Items.
// AppDbContext lo lee al crearse: sin sesión válida no hay contexto y los filtros no devuelven datos.
public sealed class TenantActualHttp(IHttpContextAccessor accesor) : ITenantActual
{
    public const string Clave = "UltimaMilla.Tenant";

    public TenantContext? Contexto => accesor.HttpContext?.Items[Clave] as TenantContext;
}
