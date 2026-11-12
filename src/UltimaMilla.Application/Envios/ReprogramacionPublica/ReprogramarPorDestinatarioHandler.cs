using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.ReprogramacionPublica;

// Carga y guarda el envío a partir del token público. El destinatario no tiene cuenta: el envío se resuelve
// solo por su token aleatorio, igual que el seguimiento, y se opera en el ámbito de su operador.
public interface IReprogramacionPublicaRepository
{
    // null si el token no corresponde a ningún envío. Devuelve el envío con lo que necesita el dominio
    // para reprogramar (bultos, intentos y motivos, reglas, zona y franjas, programación, parada y devolución).
    Task<Envio?> CargarPorTokenAsync(string token, CancellationToken cancellationToken = default);

    // Lanza ConflictoConcurrenciaException si el envío cambió desde que se cargó.
    Task GuardarAsync(Envio envio, CancellationToken cancellationToken = default);
}

// Lo que ve el destinatario: si puede reprogramar y, si puede, qué fechas y franjas tiene para elegir.
public sealed record OpcionesReprogramacion(string? Impedimento, IReadOnlyList<OpcionFechaFranja> Opciones)
{
    public bool Disponible => Impedimento is null && Opciones.Count > 0;
}

public sealed record OpcionFechaFranja(DateOnly Fecha, Guid FranjaId, string Franja, DateTimeOffset Desde, DateTimeOffset Hasta)
{
    public string Clave => $"{Fecha:yyyy-MM-dd}|{FranjaId}";
}

public enum ResultadoReprogramacion
{
    Reprogramado,
    NoEncontrado,
    NoPermitido,     // las reglas del operador no lo admiten (el motivo va en el mensaje)
    OpcionInvalida,  // la fecha/franja no está entre las ofrecidas o no respeta el plazo entre intentos
    Conflicto        // el envío cambió mientras el destinatario elegía
}

// Solicitud de reprogramación por parte del destinatario desde el seguimiento público (requisito 26).
// Las reglas del operador (motivo reintentable, intentos disponibles, plazo entre intentos, política de
// entrega parcial) las aplica el dominio en Envio.ReprogramarPorDestinatario; acá solo se arma la
// programación con la fecha y franja elegidas.
public sealed class ReprogramarPorDestinatarioHandler(IReprogramacionPublicaRepository repositorio, TimeProvider reloj)
{
    public const int DiasOfrecidos = 7;

    public async Task<OpcionesReprogramacion?> OpcionesAsync(string token, CancellationToken ct = default)
    {
        var envio = await repositorio.CargarPorTokenAsync(token, ct);
        if (envio is null) return null;
        if (envio.ImpedimentoNuevaVisita() is { } impedimento) return new(impedimento, []);
        return new(null, Opciones(envio, reloj.GetUtcNow()));
    }

    public async Task<(ResultadoReprogramacion Resultado, string? Mensaje)> ReprogramarAsync(string token, string clave,
        CancellationToken ct = default)
    {
        var envio = await repositorio.CargarPorTokenAsync(token, ct);
        if (envio is null) return (ResultadoReprogramacion.NoEncontrado, null);
        if (envio.ImpedimentoNuevaVisita() is { } impedimento) return (ResultadoReprogramacion.NoPermitido, impedimento);

        var ahora = reloj.GetUtcNow();
        var elegida = Opciones(envio, ahora).SingleOrDefault(o => o.Clave == clave);
        if (elegida is null) return (ResultadoReprogramacion.OpcionInvalida, "La fecha elegida ya no está disponible.");

        var franja = envio.Zona.Franjas.Single(f => f.Id == elegida.FranjaId);
        var nueva = ProgramacionEntrega.Crear(franja, elegida.Fecha, elegida.Desde.Offset);
        try
        {
            envio.ReprogramarPorDestinatario(nueva, ahora.UtcDateTime);
        }
        catch (InvalidOperationException error)
        {
            // Por ejemplo, la ventana no respeta el plazo entre intentos que fijan las reglas.
            return (ResultadoReprogramacion.OpcionInvalida, error.Message);
        }
        try
        {
            await repositorio.GuardarAsync(envio, ct);
        }
        catch (Abstractions.ConflictoConcurrenciaException)
        {
            return (ResultadoReprogramacion.Conflicto, null);
        }
        return (ResultadoReprogramacion.Reprogramado, null);
    }

    // Fechas desde mañana, en la hora local del operador (el desfase de la programación actual), con las
    // franjas de la zona del envío. El plazo entre intentos lo controla el dominio al confirmar.
    private static List<OpcionFechaFranja> Opciones(Envio envio, DateTimeOffset ahora)
    {
        var desfase = envio.Programacion.InicioLocal.Offset;
        var manana = DateOnly.FromDateTime(ahora.ToOffset(desfase).Date).AddDays(1);
        var opciones = new List<OpcionFechaFranja>();
        for (var d = 0; d < DiasOfrecidos; d++)
        {
            var fecha = manana.AddDays(d);
            foreach (var f in envio.Zona.Franjas.OrderBy(f => f.Desde))
                opciones.Add(new OpcionFechaFranja(fecha, f.Id, f.Nombre,
                    new DateTimeOffset(fecha.ToDateTime(f.Desde), desfase), new DateTimeOffset(fecha.ToDateTime(f.Hasta), desfase)));
        }
        return opciones;
    }
}
