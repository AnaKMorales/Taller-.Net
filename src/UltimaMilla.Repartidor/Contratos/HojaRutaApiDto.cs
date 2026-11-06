namespace UltimaMilla.Repartidor.Contratos;

internal sealed record HojaRutaApiDto(Guid RutaId, Guid Version, IReadOnlyList<ParadaApiDto> Paradas);

internal sealed record ParadaApiDto(Guid ParadaId, int Orden, Guid EnvioId, Guid VersionEsperadaEnvio,
    EstadoEnvio EstadoEnvio, DateTimeOffset VentanaDesde, DateTimeOffset VentanaHasta,
    DestinatarioApiDto Destinatario, ReglasParadaDto Reglas, IReadOnlyList<BultoParadaDto> Bultos);

internal sealed record DestinatarioApiDto(string Nombre, DireccionApiDto Direccion);

internal sealed record DireccionApiDto(string Calle, string Numero, string Localidad, string CodigoPostal);
