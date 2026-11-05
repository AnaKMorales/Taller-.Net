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

public sealed record SesionRepartidor(string Token, Guid RepartidorId, Guid OperadorId, string Nombre);

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
