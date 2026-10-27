namespace UltimaMilla.Application.Mensajeria;

// Contrato de integración versionado. No serializa el grafo de Domain ni datos del destinatario.
public sealed record EventoEnvioV1(
    Guid MensajeId,
    int Version,
    Guid OperadorId,
    Guid CuentaComercialId,
    Guid EnvioId,
    Guid EventoId,
    string TipoEvento,
    string Origen,
    string? EstadoAnterior,
    string EstadoNuevo,
    DateTime OcurridoEn,
    Guid? IdOperacion);
