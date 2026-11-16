using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Dispositivo;

// Puente con wwwroot/js/repartidor.js. Implementa el almacén local sobre IndexedDB
// (persiste aunque se cierre la app o se reinicie el celular, a diferencia de la memoria).
public sealed class Navegador(IJSRuntime js) : IAlmacenLocal, IUbicacionDispositivo
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

    public async Task<string?> LeerFotoAsync(ElementReference entrada) =>
        await (await Modulo()).InvokeAsync<string?>("leerFoto", entrada);

    public async Task LimpiarFotoAsync(ElementReference entrada) =>
        await (await Modulo()).InvokeVoidAsync("limpiarFoto", entrada);

    public async Task PrepararFirmaAsync(ElementReference lienzo) =>
        await (await Modulo()).InvokeVoidAsync("prepararFirma", lienzo);

    public async Task<string?> LeerFirmaAsync(ElementReference lienzo) =>
        await (await Modulo()).InvokeAsync<string?>("leerFirma", lienzo);

    public async Task LimpiarFirmaAsync(ElementReference lienzo) =>
        await (await Modulo()).InvokeVoidAsync("limpiarFirma", lienzo);

    // Escaneo con la cámara (BarcodeDetector). Ver Componentes/EscanerCodigo.razor.
    public async Task<bool> EscanerDisponibleAsync() =>
        await (await Modulo()).InvokeAsync<bool>("escanerDisponible");

    // null si arrancó; si no, el motivo ("no-disponible", "permiso" o "camara").
    public async Task<string?> IniciarEscaneoAsync<T>(ElementReference video, DotNetObjectReference<T> receptor) where T : class =>
        await (await Modulo()).InvokeAsync<string?>("iniciarEscaneo", video, receptor);

    public async Task DetenerEscaneoAsync() =>
        await (await Modulo()).InvokeVoidAsync("detenerEscaneo");

    // La posición es opcional: si no hay permiso o señal, la acción se registra igual sin ella.
    public async Task<GeoPuntoDto?> ObtenerUbicacionAsync()
    {
        var coordenadas = await (await Modulo()).InvokeAsync<double[]?>("ubicacion");
        return coordenadas is [var latitud, var longitud]
            ? new GeoPuntoDto(Math.Round((decimal)latitud, 6), Math.Round((decimal)longitud, 6))
            : null;
    }

    // Igual que ObtenerUbicacionAsync, pero dice por qué falló (permiso denegado o sin señal).
    public async Task<LecturaUbicacion> LeerUbicacionAsync() =>
        LecturaUbicacion.DesdeNavegador(await (await Modulo()).InvokeAsync<double[]?>("ubicacionDetallada"));
}
