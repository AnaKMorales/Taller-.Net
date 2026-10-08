using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Application.Envios.CrearEnvio;

public class CrearEnvioHandler
{
    private readonly AppDbContext _db;

    public CrearEnvioHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Envio> Handle(CrearEnvioCommand command)
    {
        // por ahora el operador va fijo, despues sale del usuario logueado
        var operadorId = Guid.Empty;

        // el telefono es opcional en el formulario, pero la columna no acepta null
        var telefono = command.DestinatarioTelefono ?? "";

        var envio = new Envio(operadorId, command.ComercioId, command.DestinatarioNombre,
            telefono, command.DireccionCalle);

        _db.Envios.Add(envio);
        await _db.SaveChangesAsync();

        return envio;
    }
}
