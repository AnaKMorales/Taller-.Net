using System.Text.Json;
using UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Repartidor.Sincronizacion;

// Sesión y hoja de ruta guardadas en el dispositivo para poder abrir la app sin conexión.
public sealed class DatosLocales(IAlmacenLocal almacen)
{
    private const string ClaveSesion = "sesion";
    private const string ClaveHoja = "hoja-de-ruta";

    // Avisa a las pantallas cuando se guarda una hoja nueva (por ejemplo, tras sincronizar en segundo plano).
    public event Action? HojaActualizada;

    public async Task<SesionRepartidor?> ObtenerSesionAsync() =>
        await almacen.LeerAsync(ClaveSesion) is { } json ? JsonSerializer.Deserialize(json, JsonRepartidor.Default.SesionRepartidor) : null;

    public Task GuardarSesionAsync(SesionRepartidor sesion) =>
        almacen.GuardarAsync(ClaveSesion, JsonSerializer.Serialize(sesion, JsonRepartidor.Default.SesionRepartidor));

    public async Task CerrarSesionAsync()
    {
        await almacen.BorrarAsync(ClaveSesion);
        await almacen.BorrarAsync(ClaveHoja);
    }

    public async Task<HojaDeRutaDto?> ObtenerHojaAsync() =>
        await almacen.LeerAsync(ClaveHoja) is { } json ? JsonSerializer.Deserialize(json, JsonRepartidor.Default.HojaDeRutaDto) : null;

    public async Task GuardarHojaAsync(HojaDeRutaDto hoja)
    {
        await almacen.GuardarAsync(ClaveHoja, JsonSerializer.Serialize(hoja, JsonRepartidor.Default.HojaDeRutaDto));
        HojaActualizada?.Invoke();
    }
}
