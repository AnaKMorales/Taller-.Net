using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Despacho;

// La identidad procede del contexto autenticado, nunca de parámetros operador/repartidor.
public interface IHojasRuta
{
    Task<IReadOnlyList<RutaDisponibleDto>> ListarAsync(CancellationToken ct = default);
    Task<HojaRutaDto?> ObtenerAsync(Guid rutaId, CancellationToken ct = default);
}

public sealed record RutaDisponibleDto(Guid RutaId, Guid Version, DateTime DespachadaEn,
    string Matricula, int ParadasPendientes);
public sealed record VehiculoHojaDto(string Matricula, decimal CapacidadPesoKg, decimal CapacidadVolumenM3);
public sealed record DireccionHojaDto(string Calle, string Numero, string Localidad, string CodigoPostal);
public sealed record DestinatarioHojaDto(string Nombre, string Telefono, DireccionHojaDto Direccion);
public sealed record MotivoHojaDto(Guid Id, string Nombre, string Descripcion, bool RequiereEvidencia, bool EsReintentable);
public sealed record ReglasHojaDto(Guid VersionReglasId, int MaxIntentos, int PlazoEntreIntentosHoras,
    IReadOnlyList<TipoPrueba> PruebasExigidas, IReadOnlyList<MotivoHojaDto> Motivos,
    bool PermiteEntregaParcial, AccionBultosPendientes AccionPendientes, decimal? ImporteRecargoNuevaVisita);
public sealed record BultoHojaDto(Guid BultoId, string Codigo, EstadoBulto Estado, decimal PesoKg, decimal VolumenM3);
public sealed record ParadaHojaDto(Guid ParadaId, int Orden, DateTime LlegadaEstimada,
    Guid EnvioId, Guid VersionEsperadaEnvio, EstadoEnvio EstadoEnvio,
    DateTime VentanaDesde, DateTime VentanaHasta, DestinatarioHojaDto Destinatario,
    ReglasHojaDto Reglas, IReadOnlyList<BultoHojaDto> Bultos);
public sealed record HojaRutaDto(Guid RutaId, Guid Version, DateTime DespachadaEn,
    VehiculoHojaDto Vehiculo, IReadOnlyList<ParadaHojaDto> Paradas);
