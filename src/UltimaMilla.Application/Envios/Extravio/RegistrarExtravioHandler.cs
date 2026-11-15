using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Envios.Extravio;

public sealed record RegistrarExtravioCommand(Guid VersionEsperadaEnvio, Guid? VersionEsperadaRuta, string Detalle);
public sealed record BultoExtravioDto(Guid Id, string Codigo, EstadoBulto Estado);
public sealed record ConsultaExtravioDto(Guid EnvioId, string CodigoSeguimiento, EstadoEnvio Estado, Guid VersionEnvio,
    Guid? RutaId, Guid? VersionRuta, IReadOnlyList<BultoExtravioDto> Bultos);
public sealed record ExtravioRegistradoDto(Guid EnvioId, EstadoEnvio Estado, Guid VersionEnvio, Guid EventoId,
    DateTime OcurridoEn, Guid? RutaAfectadaId, Guid? VersionRuta, IReadOnlyList<BultoExtravioDto> Bultos);

public interface IExtravioRepository
{
    Task<IOperacionExtravio> AbrirAsync(Guid envioId, CancellationToken ct);
}
public interface IOperacionExtravio : IAsyncDisposable
{
    Envio Envio { get; }
    Ruta? Ruta { get; }
    Usuario Responsable { get; }
    Guid VersionEnvio { get; }
    Guid? VersionRuta { get; }
    Task GuardarAsync(CancellationToken ct);
}

public sealed class RegistrarExtravioHandler(IExtravioRepository repositorio, TimeProvider reloj)
{
    public async Task<ConsultaExtravioDto> ConsultarAsync(Guid envioId, CancellationToken ct = default)
    {
        ValidarId(envioId);
        await using var op = await repositorio.AbrirAsync(envioId, ct);
        return new(op.Envio.Id, op.Envio.CodigoSeguimiento, op.Envio.Estado, op.VersionEnvio,
            op.Ruta?.Id, op.VersionRuta, Bultos(op.Envio));
    }

    public async Task<ExtravioRegistradoDto> Handle(Guid envioId, RegistrarExtravioCommand solicitud, CancellationToken ct = default)
    {
        ValidarId(envioId); ArgumentNullException.ThrowIfNull(solicitud);
        if (solicitud.VersionEsperadaEnvio == Guid.Empty || solicitud.VersionEsperadaRuta == Guid.Empty ||
            string.IsNullOrWhiteSpace(solicitud.Detalle) || solicitud.Detalle.Trim().Length > 2000)
            throw new ArgumentException("La versión del envío y un detalle de hasta 2000 caracteres son obligatorios; la versión de ruta debe ser válida si se indica.");
        await using var op = await repositorio.AbrirAsync(envioId, ct);
        if (op.Ruta is not null && solicitud.VersionEsperadaRuta is null)
            throw new ArgumentException("La ruta activa requiere su versión esperada.");
        if (op.VersionEnvio != solicitud.VersionEsperadaEnvio || op.VersionRuta != solicitud.VersionEsperadaRuta)
            throw new ConflictoConcurrenciaException(new InvalidOperationException("El envío o su ruta cambiaron."));
        var ahora = reloj.GetUtcNow().UtcDateTime;
        if (op.Envio.Eventos.Any(e => e.OcurridoEn > ahora))
            throw new InvalidOperationException("El extravío no puede ser anterior al último evento del envío.");
        op.Envio.MarcarExtraviado(op.Responsable, ahora, solicitud.Detalle.Trim());
        var evento = op.Envio.Eventos.Last();
        await op.GuardarAsync(ct);
        return new(op.Envio.Id, op.Envio.Estado, op.VersionEnvio, evento.Id, evento.OcurridoEn,
            op.Ruta?.Id, op.VersionRuta, Bultos(op.Envio));
    }

    private static BultoExtravioDto[] Bultos(Envio envio) => envio.Bultos.OrderBy(b => b.Codigo).ThenBy(b => b.Id)
        .Select(b => new BultoExtravioDto(b.Id, b.Codigo, b.Estado)).ToArray();
    private static void ValidarId(Guid id)
    { if (id == Guid.Empty) throw new ArgumentException("El envío es obligatorio."); }
}
