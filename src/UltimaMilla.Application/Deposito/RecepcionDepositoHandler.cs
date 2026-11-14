using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Deposito;

public sealed record EscanearRecepcionCommand(string Codigo, Guid VersionEsperada);
public sealed record RegistrarDiscrepanciaCommand(Guid VersionEsperada, TipoDiscrepancia Tipo,
    string Detalle, string DetalleDeclarado, string DetalleRecibido, Guid? BultoId = null);
public sealed record ConfirmarRecepcionCommand(Guid VersionEsperada);
public sealed record BultoRecepcionDto(Guid Id, string Codigo, decimal PesoKg, decimal AltoCm,
    decimal AnchoCm, decimal LargoCm, bool Recibido);
public sealed record DiscrepanciaRecepcionDto(Guid Id, Guid? BultoId, TipoDiscrepancia Tipo,
    string Detalle, string DetalleDeclarado, string DetalleRecibido, DateTime FechaHora);
public sealed record RecepcionDepositoDto(Guid EnvioId, string CodigoSeguimiento, Guid Version, EstadoEnvio Estado,
    IReadOnlyList<BultoRecepcionDto> Bultos, IReadOnlyList<DiscrepanciaRecepcionDto> Discrepancias)
{
    public int Esperados => Bultos.Count;
    public int Recibidos => Bultos.Count(b => b.Recibido);
    public int Faltantes => Esperados - Recibidos;
}
public sealed record RegistroRecepcionDto(RecepcionDepositoDto Recepcion, bool Repetido);

public interface IRecepcionDepositoRepository
{
    Task<IOperacionRecepcionDeposito> AbrirAsync(Guid envioId, CancellationToken ct);
}

public interface IOperacionRecepcionDeposito : IAsyncDisposable
{
    Envio Envio { get; }
    Usuario Responsable { get; }
    Guid Version { get; }
    Task GuardarAsync(CancellationToken ct);
}

public sealed class RecepcionDepositoHandler(IRecepcionDepositoRepository repositorio, TimeProvider reloj)
{
    public async Task<RecepcionDepositoDto> ConsultarAsync(Guid envioId, CancellationToken ct = default)
    {
        ValidarId(envioId);
        await using var op = await repositorio.AbrirAsync(envioId, ct);
        return Estado(op);
    }

    public async Task<RegistroRecepcionDto> EscanearAsync(Guid envioId, EscanearRecepcionCommand solicitud,
        CancellationToken ct = default)
    {
        ValidarId(envioId);
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidarVersion(solicitud.VersionEsperada);
        var codigo = Texto(solicitud.Codigo);
        await using var op = await repositorio.AbrirAsync(envioId, ct);
        ValidarAbierta(op.Envio);
        var bulto = op.Envio.Bultos.SingleOrDefault(b => string.Equals(b.Codigo, codigo, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("El código no corresponde a un bulto declarado de este envío. Registrá la discrepancia por separado.");
        var repetido = Recibido(bulto);
        if (!repetido)
        {
            ComprobarVersion(op, solicitud.VersionEsperada);
            EscaneoBulto.RegistrarRecepcion(bulto, op.Responsable, reloj.GetUtcNow().UtcDateTime);
            await op.GuardarAsync(ct);
        }
        return new(Estado(op), repetido);
    }

    public async Task<RegistroRecepcionDto> RegistrarDiscrepanciaAsync(Guid envioId, RegistrarDiscrepanciaCommand solicitud,
        CancellationToken ct = default)
    {
        ValidarId(envioId);
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidarVersion(solicitud.VersionEsperada);
        if (!Enum.IsDefined(solicitud.Tipo) || solicitud.BultoId == Guid.Empty)
            throw new ArgumentException("El tipo de discrepancia y el identificador opcional deben ser válidos.");
        var detalle = Texto(solicitud.Detalle);
        var declarado = Texto(solicitud.DetalleDeclarado);
        var recibido = Texto(solicitud.DetalleRecibido);
        await using var op = await repositorio.AbrirAsync(envioId, ct);
        ValidarAbierta(op.Envio);
        var bulto = solicitud.BultoId is null ? null : op.Envio.Bultos.SingleOrDefault(b => b.Id == solicitud.BultoId)
            ?? throw new ArgumentException("El bulto no pertenece al envío.");
        var repetido = op.Envio.Discrepancias.Any(d => d.BultoId == solicitud.BultoId && d.Tipo == solicitud.Tipo &&
            d.Detalle == detalle && d.DetalleDeclarado == declarado && d.DetalleRecibido == recibido);
        if (!repetido)
        {
            ComprobarVersion(op, solicitud.VersionEsperada);
            _ = new Discrepancia(op.Envio, bulto, solicitud.Tipo, detalle, declarado, recibido, reloj.GetUtcNow().UtcDateTime);
            await op.GuardarAsync(ct);
        }
        return new(Estado(op), repetido);
    }

    public async Task<RecepcionDepositoDto> ConfirmarAsync(Guid envioId, ConfirmarRecepcionCommand solicitud,
        CancellationToken ct = default)
    {
        ValidarId(envioId);
        ArgumentNullException.ThrowIfNull(solicitud);
        ValidarVersion(solicitud.VersionEsperada);
        await using var op = await repositorio.AbrirAsync(envioId, ct);
        ValidarAbierta(op.Envio);
        ComprobarVersion(op, solicitud.VersionEsperada);
        op.Envio.RecibirEnDeposito(op.Responsable, reloj.GetUtcNow().UtcDateTime);
        await op.GuardarAsync(ct);
        return Estado(op);
    }

    private bool Recibido(Bulto bulto) => bulto.Escaneos.Any(e => e.Tipo == TipoEscaneo.Recepcion &&
        e.FechaHora >= bulto.Envio!.FechaAdmision && e.FechaHora <= reloj.GetUtcNow().UtcDateTime);

    private RecepcionDepositoDto Estado(IOperacionRecepcionDeposito op) => new(op.Envio.Id, op.Envio.CodigoSeguimiento,
        op.Version, op.Envio.Estado, op.Envio.Bultos.OrderBy(b => b.Codigo).Select(b =>
            new BultoRecepcionDto(b.Id, b.Codigo, b.Peso, b.Alto, b.Ancho, b.Largo, Recibido(b))).ToArray(),
        op.Envio.Discrepancias.OrderBy(d => d.FechaHora).ThenBy(d => d.Id).Select(d => new DiscrepanciaRecepcionDto(
            d.Id, d.BultoId, d.Tipo, d.Detalle, d.DetalleDeclarado, d.DetalleRecibido, d.FechaHora)).ToArray());

    private static void ValidarId(Guid id)
    { if (id == Guid.Empty) throw new ArgumentException("El envío es obligatorio."); }
    private static void ValidarVersion(Guid version)
    { if (version == Guid.Empty) throw new ArgumentException("La versión esperada es obligatoria."); }
    private static void ComprobarVersion(IOperacionRecepcionDeposito op, Guid version)
    {
        if (op.Version != version)
            throw new ConflictoConcurrenciaException(new InvalidOperationException("La recepción del envío cambió."));
    }
    private static void ValidarAbierta(Envio envio)
    {
        if (envio.Estado != EstadoEnvio.Admitido)
            throw new InvalidOperationException("La recepción solo admite cambios antes de confirmar el ingreso a depósito.");
    }
    private static string Texto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length > 2000)
            throw new ArgumentException("Los textos son obligatorios y admiten hasta 2000 caracteres.");
        return texto.Trim();
    }
}
