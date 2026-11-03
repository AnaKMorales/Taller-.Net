using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.RegistrarIntento;

// No recibe operador, rol ni usuario: la identidad procede del contexto autenticado.
public sealed record RegistrarIntentoCommand(Guid EnvioId, Guid ParadaId, Guid VersionEsperada,
    Guid IdOperacion, DateTime OcurridoEn, IReadOnlyList<ResultadoBultoCommand> Bultos,
    IReadOnlyList<EvidenciaIntentoCommand> Evidencias, GeoPunto? Posicion = null);

public sealed record ResultadoBultoCommand(Guid BultoId, ResultadoIntento Resultado, Guid? MotivoId,
    IReadOnlyList<Guid> Evidencias);

// Referencia local dentro de la solicitud; no es el Id de una prueba persistida.
public sealed record EvidenciaIntentoCommand(Guid Referencia, TipoPrueba Tipo, DateTime Hora,
    GeoPunto? Posicion, string? Archivo = null, string? Texto = null);

public sealed record ResultadoBultoRegistradoDto(Guid BultoId, ResultadoIntento Resultado, Guid? MotivoId);
public sealed record IntentoRegistradoDto(Guid IntentoId, Guid EnvioId, Guid IdOperacion,
    ResultadoIntento Resultado, EstadoEnvio EstadoActualEnvio, Guid VersionActualEnvio,
    bool Repetido, IReadOnlyList<ResultadoBultoRegistradoDto> Bultos);
