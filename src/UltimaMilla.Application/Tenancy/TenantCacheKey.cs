namespace UltimaMilla.Application.Tenancy;

public static class TenantCacheKey
{
    public static string PrefijoOperador(Guid operadorId)
    {
        if (operadorId == Guid.Empty) throw new ArgumentException("El operador es obligatorio.", nameof(operadorId));
        return $"op:{operadorId:N}:";
    }

    public static string Prefijo(TenantContext contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return PrefijoOperador(contexto.OperadorId) + $"cuenta:{contexto.CuentaComercialId?.ToString("N") ?? "operador"}:";
    }

    public static string Crear(TenantContext contexto, string proposito, string recurso)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentException.ThrowIfNullOrWhiteSpace(proposito);
        ArgumentException.ThrowIfNullOrWhiteSpace(recurso);
        return Prefijo(contexto) + $"origen:{contexto.Origen}:rol:{contexto.Rol}:usuario:{contexto.UsuarioId?.ToString("N")}:" +
               $"envio:{contexto.EnvioPublicoId?.ToString("N")}:" +
               $"{Uri.EscapeDataString(proposito)}:{Uri.EscapeDataString(recurso)}";
    }
}
