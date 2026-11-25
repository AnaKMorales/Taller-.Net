using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Despacho;

public sealed record RegistrarCargaRutaCommand(string Codigo, Guid VersionEsperada, Guid? BultoId = null);
public sealed record EstadoCargaRutaDto(Guid RutaId, Guid Version, DateTime? DespachadaEn,
    IReadOnlyList<BultoPreparacionDto> Bultos)
{
    public int Esperados => Bultos.Count;
    public int Cargados => Bultos.Count(b => b.Cargado);
    public int Faltantes => Esperados - Cargados;
}
// Se escaneó al cargar un bulto que no está en la hoja de ruta: no hay que subirlo al vehículo.
public sealed class BultoSobranteException(string codigo)
    : ArgumentException($"Sobrante: el bulto {codigo} no está en la hoja de ruta de este vehículo. Separalo y no lo cargues.")
{
    public string Codigo { get; } = codigo;
}

public sealed record CargaRegistradaDto(Guid RutaId, Guid BultoId, Guid Version, bool Repetido,
    int Esperados, int Cargados, int Faltantes);

public interface IRegistroCargaRutaRepository
{
    Task<IOperacionDespachoRuta> AbrirCargaAsync(Guid rutaId, CancellationToken ct);
}

public sealed class RegistrarCargaRutaHandler(IRegistroCargaRutaRepository repositorio, TimeProvider reloj)
{
    public async Task<EstadoCargaRutaDto> ConsultarAsync(Guid rutaId, CancellationToken ct = default)
    {
        ValidarRuta(rutaId);
        await using var op = await repositorio.AbrirCargaAsync(rutaId, ct);
        return Estado(op);
    }

    public async Task<CargaRegistradaDto> Handle(Guid rutaId, RegistrarCargaRutaCommand solicitud, CancellationToken ct = default)
    {
        ValidarRuta(rutaId);
        ArgumentNullException.ThrowIfNull(solicitud);
        if (string.IsNullOrWhiteSpace(solicitud.Codigo) || solicitud.VersionEsperada == Guid.Empty || solicitud.BultoId == Guid.Empty)
            throw new ArgumentException("El código y la versión son obligatorios; el identificador opcional debe ser válido.");
        await using var op = await repositorio.AbrirCargaAsync(rutaId, ct);
        if (op.Ruta.DespachadaEn is not null || op.Ruta.Rendicion is not null)
            throw new InvalidOperationException("La ruta ya no admite cargas.");
        var candidatos = op.Ruta.Paradas.Where(p => p.Activa).SelectMany(p => p.Bultos)
            .Select(b => b.Bulto).Where(b => string.Equals(b.Codigo, solicitud.Codigo.Trim(), StringComparison.OrdinalIgnoreCase) &&
                (solicitud.BultoId is null || b.Id == solicitud.BultoId)).ToArray();
        if (candidatos.Length == 0) throw new BultoSobranteException(solicitud.Codigo.Trim());
        if (candidatos.Length > 1) throw new InvalidOperationException("El código aparece en varios envíos. Indicá también el BultoId de la consulta.");
        var bulto = candidatos[0];
        var estado = Estado(op);
        var repetido = estado.Bultos.Single(b => b.BultoId == bulto.Id).Cargado;
        if (!repetido)
        {
            if (op.Version != solicitud.VersionEsperada)
                throw new ConflictoConcurrenciaException(new InvalidOperationException("La carga de la ruta cambió."));
            var ahora = reloj.GetUtcNow().UtcDateTime;
            if (ahora < PreparacionCargaRuta.UltimaAsignacion(bulto.Envio!))
                throw new InvalidOperationException("La carga no puede ser anterior a la asignación.");
            EscaneoBulto.RegistrarCarga(op.Ruta, bulto, op.Responsable, ahora);
            await op.GuardarAsync(ct);
            estado = Estado(op);
        }
        return new(rutaId, bulto.Id, op.Version, repetido, estado.Esperados, estado.Cargados, estado.Faltantes);
    }

    private EstadoCargaRutaDto Estado(IOperacionDespachoRuta op) => new(op.Ruta.Id, op.Version,
        op.Ruta.DespachadaEn, PreparacionCargaRuta.Bultos(op.Ruta, reloj.GetUtcNow().UtcDateTime));

    private static void ValidarRuta(Guid id)
    { if (id == Guid.Empty) throw new ArgumentException("La ruta es obligatoria."); }
}

internal static class PreparacionCargaRuta
{
    internal static DateTime? UltimaAsignacion(Envio envio) => envio.Eventos
        .Where(v => v.Tipo is TipoEvento.Asignar or TipoEvento.Reasignar).Max(v => (DateTime?)v.OcurridoEn);

    internal static BultoPreparacionDto[] Bultos(Ruta ruta, DateTime ahora) => ruta.Paradas.Where(p => p.Activa).OrderBy(p => p.Cod)
        .SelectMany(p => p.Bultos.OrderBy(b => b.Bulto.Codigo).Select(b => new BultoPreparacionDto(b.Bulto.Id,
            b.Bulto.Codigo, ruta.Escaneos.Any(e => e.BultoId == b.Bulto.Id && e.Tipo == TipoEscaneo.Carga &&
                e.FechaHora <= ahora && e.FechaHora >= UltimaAsignacion(p.Envio))))).ToArray();
}
