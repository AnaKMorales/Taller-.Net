using UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Repartidor.Sincronizacion;

public sealed record ResumenSincronizacion(int Sincronizadas, int Rechazadas, bool SinConexion);

// Envía al servidor las acciones pendientes, de la más antigua a la más nueva.
// - Éxito o "ya procesada" (mismo IdOperacion) => Sincronizada. Reenviar nunca duplica.
// - Rechazo de negocio => Rechazada, sin reintento (política del ADR-005).
// - Error de red/servidor => sigue Pendiente, con espera creciente, y se corta la ronda:
//   si una falló por red, las siguientes también fallarían.
public sealed class Sincronizador(ColaAcciones cola, IApiRepartidor api, TimeProvider reloj)
{
    public static readonly TimeSpan EsperaMaxima = TimeSpan.FromMinutes(5);

    // Evita dos rondas simultáneas (evento "online" + temporizador + botón), que enviarían lo mismo dos veces.
    private readonly SemaphoreSlim _enCurso = new(1, 1);

    // ignorarEspera: true cuando vuelve la conexión o el repartidor toca "Sincronizar ahora".
    public async Task<ResumenSincronizacion> SincronizarAsync(SesionRepartidor sesion, bool ignorarEspera = false,
        CancellationToken ct = default)
    {
        if (!await _enCurso.WaitAsync(0, ct)) return new ResumenSincronizacion(0, 0, false);
        try
        {
            int sincronizadas = 0, rechazadas = 0;
            var ahora = reloj.GetUtcNow();
            var pendientes = (await cola.ListarAsync())
                .Where(a => a.Estado == EstadoAccion.Pendiente)
                .Where(a => ignorarEspera || a.ProximoIntentoEn is null || a.ProximoIntentoEn <= ahora)
                .OrderBy(a => a.Accion.OcurridoEn);

            foreach (var item in pendientes)
            {
                RespuestaAccion respuesta;
                try
                {
                    respuesta = await api.EnviarAccionAsync(sesion, item.Accion, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                {
                    var intentos = item.Intentos + 1;
                    await cola.ActualizarAsync(item with
                    {
                        Intentos = intentos,
                        ProximoIntentoEn = reloj.GetUtcNow() + Espera(intentos),
                        Detalle = "No se pudo contactar al servidor; se reintentará."
                    });
                    return new ResumenSincronizacion(sincronizadas, rechazadas, true);
                }

                if (respuesta.Resultado == ResultadoAccion.Rechazada)
                {
                    rechazadas++;
                    await cola.ActualizarAsync(item with
                    {
                        Estado = EstadoAccion.Rechazada,
                        ProximoIntentoEn = null,
                        Detalle = respuesta.Detalle ?? "El servidor rechazó la acción."
                    });
                }
                else
                {
                    sincronizadas++;
                    await cola.ActualizarAsync(item with
                    {
                        Estado = EstadoAccion.Sincronizada,
                        ProximoIntentoEn = null,
                        Detalle = respuesta.Resultado == ResultadoAccion.YaProcesada ? "El servidor ya la tenía registrada." : null
                    });
                }
            }

            return new ResumenSincronizacion(sincronizadas, rechazadas, false);
        }
        finally
        {
            _enCurso.Release();
        }
    }

    // 5 s, 10 s, 20 s, 40 s... hasta 5 minutos: no satura el servidor ni la batería si la red sigue caída.
    public static TimeSpan Espera(int intentos) =>
        TimeSpan.FromSeconds(Math.Min(5 * Math.Pow(2, Math.Max(intentos, 1) - 1), EsperaMaxima.TotalSeconds));
}
