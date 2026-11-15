using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Despacho;

public sealed record AsignarEnvioRutaCommand(Guid EnvioId, Guid VersionEsperadaEnvio, Guid VersionEsperadaRuta,
    int Orden, DateTimeOffset LlegadaEstimada);
public sealed record EnvioAsignableDto(Guid Id, string CodigoSeguimiento, Guid Version, EstadoEnvio Estado,
    int Bultos, decimal PesoKg, decimal VolumenM3, DateTime VentanaDesde, DateTime VentanaHasta);
public sealed record RutaAsignacionDto(Guid Id, Guid Version, string Repartidor, string Matricula, int MaxParadas,
    int ParadasActivas, int ProximoOrden, decimal PesoOcupadoKg, decimal VolumenOcupadoM3,
    decimal CapacidadPesoKg, decimal CapacidadVolumenM3);
public sealed record EnvioAsignadoDto(Guid RutaId, Guid EnvioId, Guid ParadaId, Guid VersionEnvio, Guid VersionRuta,
    int Orden, DateTime LlegadaEstimada, IReadOnlyList<Guid> Bultos, decimal PesoKg, decimal VolumenM3);
public sealed record RecursosAsignacionDto(IReadOnlyList<RepartidorPlanificableDto> Repartidores,
    IReadOnlyList<VehiculoPlanificableDto> Vehiculos);

public interface IAsignacionEnvioRutaRepository
{
    Task<IReadOnlyList<EnvioAsignableDto>> ListarEnviosAsync(CancellationToken ct);
    Task<IReadOnlyList<RutaAsignacionDto>> ListarRutasAsync(CancellationToken ct);
    Task<RecursosAsignacionDto> RecursosAsync(CancellationToken ct);
    Task<IOperacionAsignacionEnvioRuta> AbrirAsync(Guid envioId, Guid rutaId, CancellationToken ct);
}

public interface IOperacionAsignacionEnvioRuta : IAsyncDisposable
{
    Envio Envio { get; }
    Ruta Ruta { get; }
    Usuario Responsable { get; }
    Guid VersionEnvio { get; }
    Guid VersionRuta { get; }
    Task GuardarAsync(CancellationToken ct);
}

public sealed class AsignarEnvioRutaHandler(IAsignacionEnvioRutaRepository repositorio, TimeProvider reloj)
{
    public Task<IReadOnlyList<EnvioAsignableDto>> ListarEnviosAsync(CancellationToken ct = default) => repositorio.ListarEnviosAsync(ct);
    public Task<IReadOnlyList<RutaAsignacionDto>> ListarRutasAsync(CancellationToken ct = default) => repositorio.ListarRutasAsync(ct);
    public Task<RecursosAsignacionDto> RecursosAsync(CancellationToken ct = default) => repositorio.RecursosAsync(ct);

    public async Task<EnvioAsignadoDto> Handle(Guid rutaId, AsignarEnvioRutaCommand solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (rutaId == Guid.Empty || solicitud.EnvioId == Guid.Empty || solicitud.VersionEsperadaEnvio == Guid.Empty ||
            solicitud.VersionEsperadaRuta == Guid.Empty || solicitud.Orden <= 0 || solicitud.LlegadaEstimada == default)
            throw new ArgumentException("Ruta, envío, versiones, orden positivo y llegada estimada son obligatorios.");
        await using var op = await repositorio.AbrirAsync(solicitud.EnvioId, rutaId, ct);
        if (op.VersionEnvio != solicitud.VersionEsperadaEnvio || op.VersionRuta != solicitud.VersionEsperadaRuta)
            throw new ConflictoConcurrenciaException(new InvalidOperationException("El envío o la ruta cambiaron."));
        if (op.Envio.Estado is not (EstadoEnvio.EnDeposito or EstadoEnvio.Reprogramado) ||
            op.Envio.Parada is not null || op.Envio.Devolucion is not null)
            throw new InvalidOperationException("La asignación regular requiere un envío en depósito o reprogramado, sin asignación ni devolución. Las entregas parciales usan el flujo de pendientes.");
        if (!op.Ruta.Repartidor.Activo || op.Ruta.Repartidor.Rol != RolUsuario.Repartidor)
            throw new InvalidOperationException("El repartidor de la ruta no está activo.");
        var parada = op.Ruta.AgregarParada(op.Envio, solicitud.Orden, solicitud.LlegadaEstimada.UtcDateTime,
            op.Responsable, reloj.GetUtcNow().UtcDateTime);
        await op.GuardarAsync(ct);
        return new(rutaId, op.Envio.Id, parada.Id, op.VersionEnvio, op.VersionRuta, parada.Cod, parada.HoraEstimadaLlegada,
            parada.Bultos.Select(b => b.Bulto.Id).ToArray(), parada.PesoTotalKg, parada.VolumenTotalM3);
    }
}
