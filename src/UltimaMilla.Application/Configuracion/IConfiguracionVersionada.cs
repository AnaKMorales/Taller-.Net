using UltimaMilla.Application.Tarifas;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Configuracion;

public sealed record VersionConfiguracionDto(Guid Id, int Version, DateTime VigenteDesde, bool EnUso);
public sealed record ZonaConfiguracionDto(Guid Id, string Nombre, string CodigoPostal);
public sealed record PoliticaDevolucionDto(int DiasEnDeposito, int PlazoMaximoDevolucionDias, bool DevolverAlAgotarIntentos);
public sealed record PlazoServicioDto(ModalidadServicio Modalidad, int Horas, TimeOnly HoraCorte);
public sealed record MotivoNoEntregaDto(string Nombre, string Descripcion, Responsable Responsable,
    bool RequiereEvidencia, bool EsReintentable, bool Activo);
public sealed record DatosReglasDto(int MaxIntentosEntrega, int PlazoEntreIntentosHoras,
    IReadOnlyList<TipoPrueba> TiposPrueba, PoliticaDevolucionDto Devolucion,
    IReadOnlyList<PlazoServicioDto> Plazos, IReadOnlyList<MotivoNoEntregaDto> Motivos,
    bool PermiteEntregaParcial = false,
    AccionBultosPendientes AccionPendientes = AccionBultosPendientes.NuevaVisitaSinRecargo,
    decimal? ImporteRecargoNuevaVisita = null);
public sealed record DetalleReglasDto(VersionConfiguracionDto Version, DatosReglasDto Datos);
public sealed record DetalleTarifarioDto(VersionConfiguracionDto Version, IReadOnlyList<LineaTarifaDto> Lineas);
// La versión esperada es la última creada, incluyendo las programadas a futuro; cero si no hay ninguna.
// VigenteDesde nulo significa ahora según el reloj del servidor; una fecha explícita debe ser futura y UTC.
public sealed record NuevaVersionReglas(int UltimaVersionEsperada, DateTime? VigenteDesde, DatosReglasDto Datos);
public sealed record NuevaVersionTarifario(int UltimaVersionEsperada, DateTime? VigenteDesde, IReadOnlyList<LineaTarifaDto> Lineas);
public sealed record VersionCreadaDto(VersionConfiguracionDto Version, bool RevisarInvalidacionCache = false);

// Gestión administrativa: el operador y usuario salen exclusivamente del contexto autenticado.
public interface IConfiguracionVersionada
{
    Task<IReadOnlyList<ZonaConfiguracionDto>> ListarZonasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VersionConfiguracionDto>> ListarReglasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VersionConfiguracionDto>> ListarTarifariosAsync(CancellationToken ct = default);
    Task<DetalleReglasDto?> ObtenerReglasAsync(Guid id, CancellationToken ct = default);
    Task<DetalleTarifarioDto?> ObtenerTarifarioAsync(Guid id, CancellationToken ct = default);
    Task<DetalleReglasDto?> ReglasVigentesAsync(CancellationToken ct = default);
    Task<DetalleTarifarioDto?> TarifarioVigenteAsync(CancellationToken ct = default);
    Task<VersionCreadaDto> CrearReglasAsync(NuevaVersionReglas solicitud, CancellationToken ct = default);
    Task<VersionCreadaDto> CrearTarifarioAsync(NuevaVersionTarifario solicitud, CancellationToken ct = default);
}
