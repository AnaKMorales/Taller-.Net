using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Web.Seguridad;

public static class Politicas
{
    public const string Backoffice = "Backoffice";
    public const string Portal = "Portal";

    public static readonly string[] RolesBackoffice =
        [nameof(RolUsuario.Administrador), nameof(RolUsuario.Despachador), nameof(RolUsuario.OperarioDeposito)];

    // El repartidor usa su app, no esta web.
    public static string? Inicio(RolUsuario rol) => rol switch
    {
        RolUsuario.Administrador or RolUsuario.Despachador or RolUsuario.OperarioDeposito => "/backoffice/tablero",
        RolUsuario.Comercio => "/portal",
        _ => null
    };
}

// Claims que espera TenantResolver.
public static class ClaimsUltimaMilla
{
    public const string Operador = "operador_id";
    public const string CuentaComercial = "cuenta_comercial_id";
}

// El middleware de Program.cs deja el contexto del operador en HttpContext.Items.
public sealed class TenantActualHttp(IHttpContextAccessor accesor) : ITenantActual
{
    public const string Clave = "UltimaMilla.Tenant";

    public TenantContext? Contexto => accesor.HttpContext?.Items[Clave] as TenantContext;
}
