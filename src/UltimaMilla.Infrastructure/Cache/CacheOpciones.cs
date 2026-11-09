namespace UltimaMilla.Infrastructure.Cache;

// Los dos perfiles de caché del ADR-004. Se pueden ajustar desde la sección "Cache" de appsettings.
public sealed class CacheOpciones
{
    // El tarifario cambia poco y cada cambio invalida la entrada; la duración es un límite de seguridad.
    public TimeSpan DuracionTarifario { get; set; } = TimeSpan.FromMinutes(30);

    // El seguimiento se invalida con cada cambio del envío (InvalidarSeguimientoInterceptor);
    // la duración es el desfase máximo si una invalidación falla.
    public TimeSpan DuracionSeguimiento { get; set; } = TimeSpan.FromSeconds(30);
}
