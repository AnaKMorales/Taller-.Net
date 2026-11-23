using System.Text.Json;

namespace UltimaMilla.Repartidor.Contratos;

internal sealed record EstadoBiometriaDto(bool Configurada);
internal sealed record OpcionesBiometriaDto(Guid DesafioId, string OpcionesJson);
internal sealed record PrepararRegistroBiometriaDto(string Contrasena);
internal sealed record ConfirmarRegistroBiometriaDto(Guid DesafioId, JsonElement Credencial);
internal sealed record CambioContrasenaBiometriaDto(Guid DesafioId, JsonElement Credencial, string Actual, string Nueva);
internal sealed record ProblemaBiometriaDto(string? Title);
