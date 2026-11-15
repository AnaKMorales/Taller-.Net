using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Deposito;

public sealed record VersionDevolucionCommand(Guid VersionEsperada);
public sealed record EscanearDevolucionCommand(string Codigo, Guid VersionEsperada);
public sealed record BultoDevolucionDto(Guid Id, string Codigo, EstadoBulto Estado, bool Recibido);
public sealed record DevolucionDepositoDto(Guid Id, EstadoDevolucion Estado, DateTime FechaInicio,
    DateTime PlazoLimite, DateTime? FechaCierre, IReadOnlyList<BultoDevolucionDto> Bultos)
{
    public int Esperados => Bultos.Count;
    public int Recibidos => Bultos.Count(b => b.Recibido);
    public int Faltantes => Esperados - Recibidos;
}
public sealed record EstadoDevolucionEnvioDto(Guid EnvioId, string CodigoSeguimiento, Guid Version,
    EstadoEnvio Estado, DevolucionDepositoDto? Devolucion);
public sealed record EscaneoDevolucionDto(EstadoDevolucionEnvioDto Envio, bool Repetido);
public sealed record DevolucionAbiertaDto(Guid EnvioId, string CodigoSeguimiento, Guid Version, Guid DevolucionId,
    DateTime FechaInicio, DateTime PlazoLimite);

public interface IDevolucionDepositoRepository
{
    Task<IReadOnlyList<DevolucionAbiertaDto>> ListarAbiertasAsync(CancellationToken ct);
    Task<IOperacionDevolucionDeposito> AbrirAsync(Guid envioId, CancellationToken ct);
}
public interface IOperacionDevolucionDeposito : IAsyncDisposable
{
    Envio Envio { get; }
    Usuario Responsable { get; }
    Guid Version { get; }
    Task GuardarAsync(CancellationToken ct);
}

public sealed class DevolucionDepositoHandler(IDevolucionDepositoRepository repositorio, TimeProvider reloj)
{
    public Task<IReadOnlyList<DevolucionAbiertaDto>> ListarAbiertasAsync(CancellationToken ct = default) =>
        repositorio.ListarAbiertasAsync(ct);

    public async Task<EstadoDevolucionEnvioDto> ConsultarAsync(Guid envioId, CancellationToken ct = default)
    {
        ValidarId(envioId);
        await using var op = await repositorio.AbrirAsync(envioId, ct);
        return Estado(op);
    }

    public async Task<EstadoDevolucionEnvioDto> IniciarAsync(Guid envioId, VersionDevolucionCommand solicitud,
        CancellationToken ct = default)
    {
        ValidarId(envioId);
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidarVersion(solicitud.VersionEsperada);
        await using var op = await repositorio.AbrirAsync(envioId, ct);
        ComprobarVersion(op, solicitud.VersionEsperada);
        op.Envio.IniciarDevolucion(op.Responsable, reloj.GetUtcNow().UtcDateTime);
        await op.GuardarAsync(ct);
        return Estado(op);
    }

    public async Task<EscaneoDevolucionDto> EscanearAsync(Guid envioId, EscanearDevolucionCommand solicitud,
        CancellationToken ct = default)
    {
        ValidarId(envioId);
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidarVersion(solicitud.VersionEsperada);
        if (string.IsNullOrWhiteSpace(solicitud.Codigo)) throw new ArgumentException("El código es obligatorio.");
        await using var op = await repositorio.AbrirAsync(envioId, ct);
        var devolucion = Abierta(op.Envio);
        var bulto = devolucion.Bultos.Select(b => b.Bulto).SingleOrDefault(b =>
            string.Equals(b.Codigo, solicitud.Codigo.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("El código no pertenece a los bultos de esta devolución.");
        var repetido = Recibido(devolucion, bulto);
        if (!repetido)
        {
            ComprobarVersion(op, solicitud.VersionEsperada);
            EscaneoBulto.RegistrarDevolucion(devolucion, bulto, op.Responsable, reloj.GetUtcNow().UtcDateTime);
            await op.GuardarAsync(ct);
        }
        return new(Estado(op), repetido);
    }

    public async Task<EstadoDevolucionEnvioDto> CompletarAsync(Guid envioId, VersionDevolucionCommand solicitud,
        CancellationToken ct = default)
    {
        ValidarId(envioId);
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidarVersion(solicitud.VersionEsperada);
        await using var op = await repositorio.AbrirAsync(envioId, ct);
        ComprobarVersion(op, solicitud.VersionEsperada);
        Abierta(op.Envio).Completar(op.Responsable, reloj.GetUtcNow().UtcDateTime);
        await op.GuardarAsync(ct);
        return Estado(op);
    }

    private bool Recibido(Devolucion devolucion, Bulto bulto) => bulto.Escaneos.Any(e =>
        e.Tipo == TipoEscaneo.Devolucion && e.DevolucionId == devolucion.Id &&
        e.FechaHora >= devolucion.FechaInicio && e.FechaHora <= reloj.GetUtcNow().UtcDateTime);
    private EstadoDevolucionEnvioDto Estado(IOperacionDevolucionDeposito op)
    {
        var d = op.Envio.Devolucion;
        return new(op.Envio.Id, op.Envio.CodigoSeguimiento, op.Version, op.Envio.Estado,
            d is null ? null : new(d.Id, d.Estado, d.FechaInicio, d.PlazoLimite, d.FechaCierre,
                d.Bultos.OrderBy(b => b.Bulto.Codigo).Select(b => new BultoDevolucionDto(b.Bulto.Id,
                    b.Bulto.Codigo, b.Bulto.Estado, Recibido(d, b.Bulto))).ToArray()));
    }
    private static Devolucion Abierta(Envio envio) => envio.Devolucion is { Estado: EstadoDevolucion.EnProceso } d
        ? d : throw new InvalidOperationException("El envío no tiene una devolución abierta.");
    private static void ValidarId(Guid id)
    { if (id == Guid.Empty) throw new ArgumentException("El envío es obligatorio."); }
    private static void ValidarVersion(Guid version)
    { if (version == Guid.Empty) throw new ArgumentException("La versión esperada es obligatoria."); }
    private static void ComprobarVersion(IOperacionDevolucionDeposito op, Guid version)
    {
        if (op.Version != version)
            throw new ConflictoConcurrenciaException(new InvalidOperationException("La devolución del envío cambió."));
    }
}
