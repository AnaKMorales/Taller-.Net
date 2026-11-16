namespace UltimaMilla.Repartidor.Contratos;

// Envío de las lecturas periódicas del GPS. Aparte de IApiRepartidor: es un canal auxiliar y su fallo no afecta
// a las entregas. Sin conexión o error del servidor lanza HttpRequestException y el lote espera a la próxima ronda.
public interface IApiPosiciones
{
    Task<PosicionesRegistradasDto> EnviarPosicionesAsync(SesionRepartidor sesion, LotePosicionesDto lote,
        CancellationToken ct = default);
}

// Posición actual del dispositivo, o null si no hay permiso o señal. En el navegador, Dispositivo/Navegador.cs.
public interface IUbicacionDispositivo
{
    Task<GeoPuntoDto?> ObtenerUbicacionAsync();
}
