using UltimaMilla.Domain.Configuracion;

namespace UltimaMilla.Application.Despacho;

public interface IDespachoPendientes
{
    Task<PlanificacionPendientesDto> ConsultarAsync(Guid envioId, CancellationToken ct);
    Task<RutaPlanificableDto> CrearRutaAsync(CrearRutaPendientes solicitud, CancellationToken ct);
    Task<VisitaPendientesDto> AsignarAsync(Guid rutaId, AsignarPendientes solicitud, CancellationToken ct);
    Task<DevolucionPendientesDto> DevolverAsync(Guid envioId, Guid versionEsperadaEnvio, CancellationToken ct);
}

public sealed record CrearRutaPendientes(Guid RepartidorId, Guid VehiculoId, int MaxParadas);
public sealed record AsignarPendientes(Guid EnvioId, Guid VersionEsperadaEnvio, Guid VersionEsperadaRuta,
    int Orden, DateTimeOffset LlegadaEstimada, ProgramacionPendientes Programacion);
public sealed record ProgramacionPendientes(Guid FranjaId, DateOnly Fecha, DateTimeOffset InicioLocal, DateTimeOffset FinLocal);

public sealed record PlanificacionPendientesDto(Guid EnvioId, Guid VersionEnvio, AccionBultosPendientes Accion,
    decimal? ImporteRecargo, IReadOnlyList<FranjaPendientesDto> Franjas, IReadOnlyList<RutaPlanificableDto> Rutas,
    IReadOnlyList<RepartidorPlanificableDto> Repartidores, IReadOnlyList<VehiculoPlanificableDto> Vehiculos);
public sealed record FranjaPendientesDto(Guid Id, string Nombre, TimeOnly Desde, TimeOnly Hasta);
public sealed record RutaPlanificableDto(Guid Id, Guid Version, string Repartidor, string Matricula, int ProximoOrden);
public sealed record RepartidorPlanificableDto(Guid Id, string Email);
public sealed record VehiculoPlanificableDto(Guid Id, string Matricula, decimal CapacidadPesoKg, decimal CapacidadVolumenM3);
public sealed record VisitaPendientesDto(Guid ParadaId, Guid VersionEnvio, Guid VersionRuta,
    IReadOnlyList<Guid> Bultos, decimal PesoKg, decimal VolumenM3, decimal RecargoPrevisto);
public sealed record DevolucionPendientesDto(Guid Id, Guid VersionEnvio, IReadOnlyList<Guid> Bultos);
