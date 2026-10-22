using UltimaMilla.Domain.Comercios;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Envios.CrearEnvio;

public interface IAltaEnvioRepository
{
    Task<ReferenciasAltaEnvio> CargarAsync(CrearEnvioCommand command, CancellationToken cancellationToken);
    Task GuardarAsync(Envio envio, CancellationToken cancellationToken);
}

public sealed record ReferenciasAltaEnvio(CuentaComercial Cuenta, ZonaCobertura Zona,
    ReglasOperativas Reglas, CuadroTarifario Tarifario, Usuario Responsable);
