using System.Security.Claims;

namespace UltimaMilla.Application.Tenancy;

public interface ITenantResolver
{
    Task<TenantContext> DesdeClaimsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<TenantContext> DesdeMensajeAsync(TenantMessageScope mensaje, CancellationToken cancellationToken = default);
}
