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

    // Avisa cuando se guarda la sesión (ingreso o vencimiento) para actualizar el indicador.
    public event Action? SesionActualizada;

    public async Task GuardarSesionAsync(SesionRepartidor sesion)
    {
        await almacen.GuardarAsync(ClaveSesion, JsonSerializer.Serialize(sesion, JsonRepartidor.Default.SesionRepartidor));
        SesionActualizada?.Invoke();
    }

    // El servidor rechazó el token: se conserva la sesión (para seguir viendo la hoja sin conexión)
    // pero queda vencida, así no se envían más pedidos hasta volver a ingresar.
    public async Task MarcarSesionVencidaAsync(SesionRepartidor sesion)
    {
        if (await EsSesionActualAsync(sesion))
            await GuardarSesionAsync(sesion with { ExpiraEn = DateTimeOffset.UnixEpoch });
    }

    public async Task CerrarSesionAsync()
    {
        var sesion = await ObtenerSesionAsync();
        await almacen.BorrarAsync(ClaveSesion);
        if (sesion is not null) await almacen.BorrarAsync(ClaveHojaDe(sesion));
        await almacen.BorrarAsync(ClaveHoja);
    }

    public async Task<HojaDeRutaDto?> ObtenerHojaAsync()
    {
        var sesion = await ObtenerSesionAsync();
        if (sesion is null) return null;
        var json = await almacen.LeerAsync(ClaveHojaDe(sesion));
        return json is null ? null : JsonSerializer.Deserialize(json, JsonRepartidor.Default.HojaDeRutaDto);
    }

    public async Task GuardarHojaAsync(SesionRepartidor sesion, HojaDeRutaDto hoja)
    {
        if (!await EsSesionActualAsync(sesion)) return;
        await almacen.GuardarAsync(ClaveHojaDe(sesion), JsonSerializer.Serialize(hoja, JsonRepartidor.Default.HojaDeRutaDto));
        HojaActualizada?.Invoke();
    }

    private static string ClaveHojaDe(SesionRepartidor sesion) => $"{ClaveHoja}:{sesion.RepartidorId}";

    private async Task<bool> EsSesionActualAsync(SesionRepartidor sesion) =>
        await ObtenerSesionAsync() is { } actual && actual.RepartidorId == sesion.RepartidorId && actual.Token == sesion.Token;
}
