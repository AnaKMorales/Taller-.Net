using Microsoft.JSInterop;
using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Dispositivo;

// Puente con wwwroot/js/repartidor.js. Implementa el almacén local sobre IndexedDB
// (persiste aunque se cierre la app o se reinicie el celular, a diferencia de la memoria).
public sealed class Navegador(IJSRuntime js) : IAlmacenLocal
{
    private Task<IJSObjectReference>? _modulo;

    private Task<IJSObjectReference> Modulo() =>
        _modulo ??= js.InvokeAsync<IJSObjectReference>("import", "./js/repartidor.js").AsTask();

    public async Task<string?> LeerAsync(string clave) =>
        await (await Modulo()).InvokeAsync<string?>("leer", clave);

    public async Task GuardarAsync(string clave, string valor) =>
        await (await Modulo()).InvokeVoidAsync("guardar", clave, valor);

    public async Task BorrarAsync(string clave) =>
        await (await Modulo()).InvokeVoidAsync("borrar", clave);

    public async Task<bool> EscucharConexionAsync(DotNetObjectReference<EstadoConexion> receptor) =>
        await (await Modulo()).InvokeAsync<bool>("escucharConexion", receptor);

    // La posición es opcional: si no hay permiso o señal, la acción se registra igual sin ella.
    public async Task<GeoPuntoDto?> ObtenerUbicacionAsync()
    {
        var coordenadas = await (await Modulo()).InvokeAsync<double[]?>("ubicacion");
        return coordenadas is [var latitud, var longitud]
            ? new GeoPuntoDto(Math.Round((decimal)latitud, 6), Math.Round((decimal)longitud, 6))
            : null;
    }
}
