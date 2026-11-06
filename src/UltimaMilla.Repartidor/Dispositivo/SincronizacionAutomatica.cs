using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Dispositivo;

// Decide CUÁNDO sincronizar; el CÓMO está en Sincronizador (probado sin navegador).
// Momentos: al volver la conexión, cada 30 segundos, después de registrar una acción y con el botón.
public sealed class SincronizacionAutomatica(EstadoConexion conexion, Sincronizador sincronizador,
    DatosLocales datos, IApiRepartidor api, TimeProvider reloj, ILogger<SincronizacionAutomatica> log)
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
        SesionRepartidor? sesion = null;
        // Corre en segundo plano: un error se registra pero no puede tirar la app.
        try
        {
            if (!conexion.EnLinea || (sesion = await datos.ObtenerSesionAsync()) is null) return;
            // Con la sesión vencida no se envía nada: las acciones esperan a que el repartidor vuelva a ingresar.
            if (sesion.Vencida(reloj.GetUtcNow())) return;
            var resumen = await sincronizador.SincronizarAsync(sesion, ignorarEspera);
            if (resumen.SesionExpirada)
            {
                await datos.MarcarSesionVencidaAsync(sesion);
                return;
            }

            if (resumen.Sincronizadas + resumen.Rechazadas > 0 &&
                await datos.ObtenerHojaAsync() is { } hoja &&
                await datos.ObtenerSesionAsync() is { } actual &&
                actual.RepartidorId == sesion.RepartidorId && actual.Token == sesion.Token)
            {
                var actualizada = await api.ObtenerHojaDeRutaAsync(sesion, hoja.RutaId);
                if ((await datos.ObtenerHojaAsync())?.RutaId == hoja.RutaId)
                    await datos.GuardarHojaAsync(sesion, actualizada);
            }
        }
        catch (SesionExpiradaException)
        {
            if (sesion is not null) await datos.MarcarSesionVencidaAsync(sesion);
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
