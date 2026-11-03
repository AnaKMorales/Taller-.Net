namespace UltimaMilla.Application.Mensajeria;

public sealed record ResultadoBultoV2(Guid BultoId, string Resultado, Guid? MotivoId, IReadOnlyList<Guid> Pruebas);
public sealed record EventoEnvioV2(Guid MensajeId, int Version, Guid OperadorId, Guid CuentaComercialId,
    Guid EnvioId, Guid EventoId, string TipoEvento, string Origen, string? EstadoAnterior,
    string EstadoNuevo, DateTime OcurridoEn, Guid IdOperacion, IReadOnlyList<ResultadoBultoV2> Bultos);
