using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Tablero;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Web.Seguridad;

namespace UltimaMilla.Web.Tiempo;

[Authorize(Policy = Politicas.Backoffice)]
public sealed class TableroHub(ITenantResolver resolver) : Hub
{
    public const string Ruta = "/hubs/tablero";
    public const string MetodoCambios = "cambiosEnvio";

    public static string Grupo(Guid operadorId) => $"operador:{operadorId:N}";

    public override async Task OnConnectedAsync()
    {
        try
        {
            // El grupo sale de la identidad verificada, no de un dato del cliente.
            var tenant = await resolver.DesdeClaimsAsync(Context.User!, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(tenant.OperadorId), Context.ConnectionAborted);
        }
        catch (AislamientoOperadorException)
        {
            Context.Abort();
            return;
        }
        await base.OnConnectedAsync();
    }
}

public sealed class NotificadorTableroSignalR(IHubContext<TableroHub> hub) : INotificadorTablero
{
    public async Task PublicarAsync(IReadOnlyList<CambioEnvio> cambios, CancellationToken cancellationToken = default)
    {
        foreach (var delOperador in cambios.GroupBy(c => c.OperadorId))
            await hub.Clients.Group(TableroHub.Grupo(delOperador.Key))
                .SendAsync(TableroHub.MetodoCambios, delOperador.ToList(), cancellationToken);
    }
}
