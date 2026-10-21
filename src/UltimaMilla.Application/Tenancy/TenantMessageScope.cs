namespace UltimaMilla.Application.Tenancy;

// Sobre de contexto que acompaña al mensaje. El consumidor autentica primero el origen del mensaje.
public sealed record TenantMessageScope(Guid OperadorId, Guid? CuentaComercialId = null);
