using System.Text.Json;
using System.Text.Json.Serialization;
using UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Repartidor.Sincronizacion;

// Serialización generada en compilación: la publicación de WebAssembly recorta código no usado
// y la serialización por reflexión puede romperse o generar advertencias (que el CI trata como errores).
// Enums como texto, igual que la API de Web (JsonStringEnumConverter).
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SesionRepartidor))]
[JsonSerializable(typeof(SolicitudLogin))]
[JsonSerializable(typeof(TokenAcceso))]
[JsonSerializable(typeof(PerfilRepartidor))]
[JsonSerializable(typeof(HojaDeRutaDto))]
[JsonSerializable(typeof(HojaRutaApiDto))]
[JsonSerializable(typeof(RutaDisponibleDto[]))]
[JsonSerializable(typeof(AccionEntregaDto))]
[JsonSerializable(typeof(IntentoPorBultoDto))]
[JsonSerializable(typeof(IntentoConfirmadoDto))]
[JsonSerializable(typeof(ProblemaIntentoDto))]
[JsonSerializable(typeof(RespuestaAccion))]
[JsonSerializable(typeof(ReporteSincronizacionDto))]
[JsonSerializable(typeof(List<AccionPendiente>))]
[JsonSerializable(typeof(LotePosicionesDto))]
[JsonSerializable(typeof(PosicionesRegistradasDto))]
[JsonSerializable(typeof(List<LecturaPosicionDto>))]
[JsonSerializable(typeof(EstadoBiometriaDto))]
[JsonSerializable(typeof(OpcionesBiometriaDto))]
[JsonSerializable(typeof(PrepararRegistroBiometriaDto))]
[JsonSerializable(typeof(ConfirmarRegistroBiometriaDto))]
[JsonSerializable(typeof(CambioContrasenaBiometriaDto))]
[JsonSerializable(typeof(ProblemaBiometriaDto))]
[JsonSerializable(typeof(ClavePushDto))]
[JsonSerializable(typeof(SuscripcionPushDto))]
[JsonSerializable(typeof(BajaSuscripcionPushDto))]
internal sealed partial class JsonRepartidor : JsonSerializerContext;
