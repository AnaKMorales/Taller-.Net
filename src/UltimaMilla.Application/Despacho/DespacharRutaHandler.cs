using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Despacho;

public sealed record DespacharRutaCommand(Guid VersionEsperada);
public sealed record BultoPreparacionDto(Guid BultoId, string Codigo, bool Cargado);
public sealed record PreparacionRutaDto(Guid RutaId, Guid Version, DateTime? DespachadaEn,
    IReadOnlyList<BultoPreparacionDto> Bultos);
public sealed record RutaDespachadaDto(Guid RutaId, Guid Version, DateTime DespachadaEn);

public interface IDespachoRutaRepository
{
    Task<IOperacionDespachoRuta> AbrirAsync(Guid rutaId, CancellationToken ct);
}

public interface IOperacionDespachoRuta : IAsyncDisposable
{
    Ruta Ruta { get; }
    Usuario Responsable { get; }
    Guid Version { get; }
    Task GuardarAsync(CancellationToken ct);
}

public sealed class DespacharRutaHandler(IDespachoRutaRepository repositorio, TimeProvider reloj)
{
    public async Task<PreparacionRutaDto> ConsultarAsync(Guid rutaId, CancellationToken ct = default)
    {
        ValidarId(rutaId);
        await using var operacion = await repositorio.AbrirAsync(rutaId, ct);
        return new(rutaId, operacion.Version, operacion.Ruta.DespachadaEn, Bultos(operacion.Ruta));
    }

    public async Task<RutaDespachadaDto> Handle(Guid rutaId, DespacharRutaCommand solicitud, CancellationToken ct = default)
    {
        ValidarId(rutaId);
        ArgumentNullException.ThrowIfNull(solicitud);
        if (solicitud.VersionEsperada == Guid.Empty) throw new ArgumentException("La versión esperada es obligatoria.");
        await using var operacion = await repositorio.AbrirAsync(rutaId, ct);
        if (operacion.Version != solicitud.VersionEsperada)
            throw new ConflictoConcurrenciaException(new InvalidOperationException("La ruta cambió."));
        var ruta = operacion.Ruta;
        if (!ruta.Repartidor.Activo || ruta.Repartidor.Rol != RolUsuario.Repartidor)
            throw new InvalidOperationException("El repartidor de la ruta no está activo.");
        if (Bultos(ruta).Any(b => !b.Cargado))
            throw new InvalidOperationException("Falta registrar la carga de uno o más bultos de la visita.");
        ruta.Despachar(operacion.Responsable, reloj.GetUtcNow().UtcDateTime);
        await operacion.GuardarAsync(ct);
        return new(rutaId, operacion.Version, ruta.DespachadaEn!.Value);
    }

    private BultoPreparacionDto[] Bultos(Ruta ruta) => PreparacionCargaRuta.Bultos(ruta, reloj.GetUtcNow().UtcDateTime);

    private static void ValidarId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("La ruta es obligatoria.");
    }
}
