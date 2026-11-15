namespace UltimaMilla.Application.Administracion;

// Solo datos destinados a mostrarse públicamente. Nunca incluye operador/cuenta/usuario IDs.
public sealed record IdentidadOperadorDto(string Marca, string ColorPrimario, string ColorSecundario,
    string? TelefonoContacto, string? EmailContacto, string? LogoUrl);
public sealed record EstadoIdentidadOperador(IdentidadOperadorDto Datos, Guid Version);
public sealed record ModificarIdentidadOperador(IdentidadOperadorDto Datos, Guid VersionEsperada);

public interface IAdministracionIdentidadOperador
{
    Task<EstadoIdentidadOperador> ConsultarAsync(CancellationToken ct = default);
    Task<EstadoIdentidadOperador> ModificarAsync(ModificarIdentidadOperador solicitud, CancellationToken ct = default);
}

public interface IConsultaIdentidadOperador
{
    // Cuenta/operador desde identidad validada o ámbito confiable del mensaje, nunca un parámetro HTTP libre.
    Task<IdentidadOperadorDto> ActualAsync(CancellationToken ct = default);
    Task<IdentidadOperadorDto?> PorSeguimientoAsync(string token, CancellationToken ct = default);
}
