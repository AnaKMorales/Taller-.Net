using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Dispositivo;

// Decide CUÁNDO sincronizar; el CÓMO está en Sincronizador (probado sin navegador).
// Momentos: al volver la conexión, cada 30 segundos, después de registrar una acción y con el botón.
public sealed class SincronizacionAutomatica(EstadoConexion conexion, Sincronizador sincronizador,
    DatosLocales datos, IApiRepartidor api, ILogger<SincronizacionAutomatica> log)
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);
    private bool _iniciada;

    public async Task IniciarAsync()
    {
        if (_iniciada) return;
        _iniciada = true;
        await conexion.IniciarAsync();
        conexion.Cambio += () =>
        {
            if (conexion.EnLinea) _ = SincronizarAhoraAsync();
        };
        _ = BucleAsync();
        _ = SincronizarAhoraAsync();
    }

    // Ignora la espera entre reintentos: el repartidor o la red acaban de dar una señal nueva.
    public Task SincronizarAhoraAsync() => SincronizarAsync(ignorarEspera: true);

    private async Task BucleAsync()
    {
        using var temporizador = new PeriodicTimer(Intervalo);
        while (await temporizador.WaitForNextTickAsync())
            await SincronizarAsync(ignorarEspera: false);
    }

    private async Task SincronizarAsync(bool ignorarEspera)
    {
        // Corre en segundo plano: un error se registra pero no puede tirar la app.
        try
        {
            if (!conexion.EnLinea || await datos.ObtenerSesionAsync() is not { } sesion) return;
            var resumen = await sincronizador.SincronizarAsync(sesion, ignorarEspera);

            // Si el servidor aceptó o rechazó algo, se baja la hoja actualizada con los estados reales.
            if (resumen.Sincronizadas + resumen.Rechazadas > 0)
                await datos.GuardarHojaAsync(await api.ObtenerHojaDeRutaAsync(sesion));
        }
        catch (HttpRequestException)
        {
            // Sin servidor: la próxima ronda lo intenta de nuevo.
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Falló la sincronización automática.");
        }
    }
}
