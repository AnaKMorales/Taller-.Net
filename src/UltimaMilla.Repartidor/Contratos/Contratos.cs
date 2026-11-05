namespace UltimaMilla.Repartidor.Contratos;

// Copia del contrato JSON de la API del repartidor (ver docs/adr/005-sincronizacion-offline-repartidor.md).
// La PWA no referencia Domain: los nombres de los enums coinciden con los del servidor
// para que viajen como texto y el servidor los entienda sin traducción.

public enum EstadoEnvio
{
    Admitido,
    EnDeposito,
    AsignadoARuta,
    EnTransito,
    Entregado,
    NoEntregado,
    Reprogramado,
    EnDevolucion,
    Devuelto,
    Extraviado
}

public enum TipoPrueba
{
    Firma,
    Foto,
    Documento,
    Nombre
}

public enum ResultadoIntento
{
    Entregado,
    Fallido
}

// Sesión guardada en el dispositivo. No incluye el operador: el servidor lo obtiene del token
// en cada pedido, así la PWA no puede elegir ni alterar a qué operador pertenece.
public sealed record SesionRepartidor(string Token, Guid RepartidorId, string Email, DateTimeOffset ExpiraEn)
{
    public bool Vencida(DateTimeOffset ahora) => ExpiraEn <= ahora;
}

// Cuerpo de POST /api/repartidor/sesion.
public sealed record SolicitudLogin(string Email, string Contrasena);

// Respuesta del manejador Bearer de ASP.NET Core (AddBearerToken).
public sealed record TokenAcceso(string AccessToken, long ExpiresIn);

// Respuesta de GET /api/repartidor/sesion.
public sealed record PerfilRepartidor(Guid UsuarioId, string Email);

// El servidor respondió 401 o el token venció: hay que volver a ingresar. Las acciones guardadas no se pierden.
public sealed class SesionExpiradaException() : Exception("La sesión venció. Ingresá de nuevo para sincronizar.");

public sealed record GeoPuntoDto(decimal Latitud, decimal Longitud);

public sealed record MotivoNoEntregaDto(Guid Id, string Nombre, bool RequiereEvidencia);

public sealed record ParadaDto(Guid EnvioId, int Orden, string Destinatario, string Direccion,
    DateTimeOffset VentanaDesde, DateTimeOffset VentanaHasta, EstadoEnvio Estado);

// Todo lo que la app necesita para trabajar sin conexión: paradas, catálogo de motivos
// y las pruebas que exige el operador para una entrega exitosa.
public sealed record HojaDeRutaDto(Guid RutaId, DateTimeOffset DescargadaEn, IReadOnlyList<ParadaDto> Paradas,
    IReadOnlyList<MotivoNoEntregaDto> Motivos, IReadOnlyList<TipoPrueba> PruebasExigidas);

// En Domain toda prueba exige posición; acá es opcional para que el validador dé un mensaje claro.
public sealed record PruebaDto(TipoPrueba Tipo, string Valor, DateTimeOffset Hora, GeoPuntoDto? Posicion);

// Una acción registrada en el dispositivo. IdOperacion se genera acá y es la clave de idempotencia:
// el servidor ignora un segundo envío con el mismo IdOperacion (Envio.RegistrarIntento).
public sealed record AccionEntregaDto(Guid IdOperacion, Guid EnvioId, ResultadoIntento Resultado, Guid? MotivoId,
    IReadOnlyList<PruebaDto> Pruebas, DateTimeOffset OcurridoEn, GeoPuntoDto? Posicion);

public enum ResultadoAccion
{
    // La registró ahora.
    Aceptada,
    // Ya tenía ese IdOperacion: no la duplica. Para la app es un éxito.
    YaProcesada,
    // Regla de negocio o conflicto (el envío cambió de estado, plazo, motivo inactivo...). No se reintenta.
    Rechazada
}

public sealed record RespuestaAccion(ResultadoAccion Resultado, string? Detalle = null);
