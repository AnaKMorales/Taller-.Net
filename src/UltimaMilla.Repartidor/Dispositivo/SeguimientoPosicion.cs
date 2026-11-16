using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Dispositivo;

// Seguimiento de la posición del vehículo durante la jornada (req. 23): cada 2 minutos, mientras la app está
// abierta y hay sesión y hoja de ruta, toma el GPS (que funciona sin internet) y guarda la lectura en la cola
// local. Al sincronizar, manda los pendientes en lotes. Límite de la PWA: con la app cerrada o la pantalla
// apagada el navegador no deja leer el GPS (ADR-005).
public sealed class SeguimientoPosicion(DatosLocales datos, ColaPosiciones cola, IUbicacionDispositivo ubicacion,
    IApiPosiciones api, TimeProvider reloj, ILogger<SeguimientoPosicion> log)
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(2);
    public const int TamanoLote = 200;
    private bool _iniciado;

    public void Iniciar()
    {
        if (_iniciado) return;
        _iniciado = true;
        _ = BucleAsync();
    }

    private async Task BucleAsync()
    {
        await RegistrarLecturaAsync();
        using var temporizador = new PeriodicTimer(Intervalo, reloj);
        while (await temporizador.WaitForNextTickAsync())
            await RegistrarLecturaAsync();
    }

    // Devuelve true si guardó una lectura. Corre en segundo plano: nunca lanza.
    public async Task<bool> RegistrarLecturaAsync()
    {
        try
        {
            if (await datos.ObtenerSesionAsync() is not { } sesion || sesion.Vencida(reloj.GetUtcNow())) return false;
            // Sin ruta asignada (hoja vacía) no hay recorrido que seguir: el servidor la rechazaría.
            if (await datos.ObtenerHojaAsync() is not { } hoja || hoja.RutaId == Guid.Empty) return false;
            if (await ubicacion.ObtenerUbicacionAsync() is not { } punto) return false;
            await cola.AgregarAsync(sesion.RepartidorId, new LecturaPosicionDto(Guid.NewGuid(), hoja.RutaId,
                punto.Latitud, punto.Longitud, null, reloj.GetUtcNow().UtcDateTime));
            return true;
        }
        catch (Exception ex)
        {
            log.LogDebug(ex, "No se pudo registrar la posición.");
            return false;
        }
    }

    // Manda los pendientes del repartidor en lotes. Lo que el servidor contestó (aceptado, repetido o rechazado)
    // sale de la cola; si no hay conexión, queda para la próxima ronda. Lo llama SincronizacionAutomatica.
    public async Task EnviarPendientesAsync(SesionRepartidor sesion, CancellationToken ct = default)
    {
        for (var vueltas = 0; vueltas < ColaPosiciones.Maximo / TamanoLote + 1; vueltas++)
        {
            var lote = await cola.PrimerasAsync(sesion.RepartidorId, TamanoLote);
            if (lote.Count == 0) return;
            await api.EnviarPosicionesAsync(sesion, new LotePosicionesDto(lote), ct);
            await cola.QuitarAsync(sesion.RepartidorId, lote.Select(l => l.IdLectura));
        }
    }
}
