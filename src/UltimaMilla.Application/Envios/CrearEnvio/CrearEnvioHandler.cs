using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.CrearEnvio;

public sealed class CrearEnvioHandler(IAltaEnvioRepository repository, TimeProvider clock)
{
    public async Task<Envio> Handle(CrearEnvioCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CuentaComercialId == Guid.Empty || command.ZonaId == Guid.Empty ||
            command.ReglasId == Guid.Empty || command.CuadroTarifarioId == Guid.Empty ||
            command.FranjaHorariaId == Guid.Empty || command.Preparacion is null ||
            command.Bultos is null || command.Bultos.Count == 0 || command.Bultos.Any(b => b is null))
            throw new ArgumentException("Faltan los datos operativos y la programación necesarios para crear el envío.");

        var references = await repository.CargarAsync(command, cancellationToken);
        var preparation = command.Preparacion;
        if (preparation.FechaComprometida.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha comprometida debe expresarse en UTC.");
        var franja = references.Zona.Franjas.SingleOrDefault(f => f.Id == command.FranjaHorariaId)
            ?? throw new ArgumentException("La franja no pertenece a la zona seleccionada.");
        var programacion = new ProgramacionEntrega(franja, preparation.FechaEntrega,
            preparation.InicioLocal, preparation.FinLocal);
        var destinatario = new Destinatario(command.DestinatarioNombre, command.DestinatarioTelefono,
            command.DestinatarioEmail, new Direccion(command.DireccionCalle, command.DireccionNumero,
                command.DireccionLocalidad, command.DireccionCodigoPostal));
        var bultos = command.Bultos.Select(b => new Bulto(b.Codigo, b.Peso, b.Alto, b.Ancho, b.Largo)).ToArray();
        var envio = new Envio(references.Cuenta, destinatario, bultos, command.ModalidadServicio,
            references.Zona, references.Reglas, references.Tarifario, preparation.TarifaCalculada,
            clock.GetUtcNow().UtcDateTime, preparation.FechaComprometida, programacion, references.Responsable);
        await repository.GuardarAsync(envio, cancellationToken);
        return envio;
    }
}
