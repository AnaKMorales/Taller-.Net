using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Deposito;

public sealed record RendirRutaCommand(Guid VersionEsperada);
public sealed record RutaPorRendirDto(Guid RutaId, Guid Version, string Repartidor, string Matricula,
    DateTime DespachadaEn, int ParadasPendientes);
public sealed record ParadaRendicionDto(Guid ParadaId, Guid EnvioId, string CodigoSeguimiento, int Orden,
    bool Activa, EstadoEnvio EstadoEnvio, EstadoDevolucion? EstadoDevolucion);
public sealed record EstadoRendicionRutaDto(Guid RutaId, Guid Version, DateTime? DespachadaEn,
    Guid? RendicionId, DateTime? RendidaEn, IReadOnlyList<ParadaRendicionDto> Paradas)
{
    public int ParadasPendientes => Paradas.Count(p => p.Activa);
}
public interface IRendicionRutaRepository
{
    Task<IReadOnlyList<RutaPorRendirDto>> ListarAsync(CancellationToken ct);
    Task<IOperacionRendicionRuta> AbrirRutaAsync(Guid rutaId, CancellationToken ct);
}
public interface IOperacionRendicionRuta : IAsyncDisposable
{
    Ruta Ruta { get; }
    Guid Version { get; }
    Task GuardarAsync(CancellationToken ct);
}

public sealed class RendirRutaHandler(IRendicionRutaRepository repositorio, TimeProvider reloj)
{
    public Task<IReadOnlyList<RutaPorRendirDto>> ListarAsync(CancellationToken ct = default) => repositorio.ListarAsync(ct);

    public async Task<EstadoRendicionRutaDto> ConsultarAsync(Guid rutaId, CancellationToken ct = default)
    {
        ValidarId(rutaId);
        await using var op = await repositorio.AbrirRutaAsync(rutaId, ct);
        return Estado(op);
    }

    public async Task<EstadoRendicionRutaDto> Handle(Guid rutaId, RendirRutaCommand solicitud, CancellationToken ct = default)
    {
        ValidarId(rutaId);
        ArgumentNullException.ThrowIfNull(solicitud);
        if (solicitud.VersionEsperada == Guid.Empty) throw new ArgumentException("La versión esperada es obligatoria.");
        await using var op = await repositorio.AbrirRutaAsync(rutaId, ct);
        if (op.Version != solicitud.VersionEsperada)
            throw new ConflictoConcurrenciaException(new InvalidOperationException("La ruta cambió."));
        op.Ruta.Rendir(reloj.GetUtcNow().UtcDateTime);
        await op.GuardarAsync(ct);
        return Estado(op);
    }

    private static EstadoRendicionRutaDto Estado(IOperacionRendicionRuta op) => new(op.Ruta.Id, op.Version,
        op.Ruta.DespachadaEn, op.Ruta.Rendicion?.Id, op.Ruta.Rendicion?.FechaHora,
        op.Ruta.Paradas.OrderBy(p => p.Cod).Select(p => new ParadaRendicionDto(p.Id, p.EnvioId,
            p.Envio.CodigoSeguimiento, p.Cod, p.Activa, p.Envio.Estado, p.Envio.Devolucion?.Estado)).ToArray());
    private static void ValidarId(Guid id)
    { if (id == Guid.Empty) throw new ArgumentException("La ruta es obligatoria."); }
}
