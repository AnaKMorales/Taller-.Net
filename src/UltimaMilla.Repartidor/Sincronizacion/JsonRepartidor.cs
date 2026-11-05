using System.Text.Json;
using System.Text.Json.Serialization;
using UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Repartidor.Sincronizacion;

// Serialización generada en compilación: la publicación de WebAssembly recorta código no usado
// y la serialización por reflexión puede romperse o generar advertencias (que el CI trata como errores).
// Enums como texto, igual que la API de Web (JsonStringEnumConverter).
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SesionRepartidor))]
[JsonSerializable(typeof(HojaDeRutaDto))]
[JsonSerializable(typeof(AccionEntregaDto))]
[JsonSerializable(typeof(RespuestaAccion))]
[JsonSerializable(typeof(List<AccionPendiente>))]
public sealed partial class JsonRepartidor : JsonSerializerContext;
