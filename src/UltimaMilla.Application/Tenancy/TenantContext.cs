using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Tenancy;

public enum OrigenTenant { UsuarioAutenticado, MensajeWorker, SeguimientoPublico, ProcesoInterno }

// Valor inmutable. Sus fábricas reciben información previamente autenticada/autorizada.
public sealed class TenantContext
{
    public Guid OperadorId { get; }
    public Guid? CuentaComercialId { get; }
    public Guid? UsuarioId { get; }
    public RolUsuario? Rol { get; }
    public OrigenTenant Origen { get; }
    public Guid? EnvioPublicoId { get; }

    private TenantContext(Guid operadorId, Guid? cuentaId, Guid? usuarioId, RolUsuario? rol, OrigenTenant origen, Guid? envioId = null)
    {
        if (operadorId == Guid.Empty || cuentaId == Guid.Empty || usuarioId == Guid.Empty || envioId == Guid.Empty)
            throw new ArgumentException("Los identificadores del contexto deben ser válidos.");
        OperadorId = operadorId;
        CuentaComercialId = cuentaId;
        UsuarioId = usuarioId;
        Rol = rol;
        Origen = origen;
        EnvioPublicoId = envioId;
    }

    public static TenantContext ParaUsuario(Guid operadorId, Guid usuarioId, RolUsuario rol, Guid? cuentaComercialId = null)
    {
        if (!Enum.IsDefined(rol) || usuarioId == Guid.Empty || (rol == RolUsuario.Comercio) != cuentaComercialId.HasValue)
            throw new ArgumentException("El perfil de comercio requiere su cuenta; un perfil interno no lleva cuenta activa.");
        return new(operadorId, cuentaComercialId, usuarioId, rol, OrigenTenant.UsuarioAutenticado);
    }

    public static TenantContext ParaMensaje(Guid operadorId, Guid? cuentaComercialId = null) =>
        new(operadorId, cuentaComercialId, null, null, OrigenTenant.MensajeWorker);

    public static TenantContext ParaProcesoInterno(Guid operadorId) => new(operadorId, null, null, null, OrigenTenant.ProcesoInterno);

    public static TenantContext ParaSeguimiento(Guid operadorId, Guid cuentaComercialId, Guid envioId) =>
        new(operadorId, cuentaComercialId, null, null, OrigenTenant.SeguimientoPublico, envioId);
}

public sealed class TenantActual(TenantContext contexto) : ITenantActual
{
    public TenantContext Contexto { get; } = contexto ?? throw new ArgumentNullException(nameof(contexto));
    public static TenantActual ParaOperador(Guid operadorId) => new(TenantContext.ParaProcesoInterno(operadorId));
}
