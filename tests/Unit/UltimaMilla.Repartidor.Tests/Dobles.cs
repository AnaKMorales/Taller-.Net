using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Tests;

// Reemplaza IndexedDB. Compartir la instancia entre dos ColaAcciones simula cerrar y reabrir la app.
internal sealed class AlmacenEnMemoria : IAlmacenLocal
{
    private readonly Dictionary<string, string> _datos = [];
    public Task<string?> LeerAsync(string clave) => Task.FromResult(_datos.GetValueOrDefault(clave));
    public Task GuardarAsync(string clave, string valor) { _datos[clave] = valor; return Task.CompletedTask; }
    public Task BorrarAsync(string clave) { _datos.Remove(clave); return Task.CompletedTask; }
}

internal sealed class RelojManual(DateTimeOffset inicio) : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = inicio;
    public override DateTimeOffset GetUtcNow() => Ahora;
}

// Servidor simulado con la misma regla de idempotencia que Envio.RegistrarIntento.
internal sealed class ServidorEnMemoria : IApiRepartidor
{
    private readonly HashSet<Guid> _procesadas = [];
    public bool EnLinea { get; set; } = true;
    // Registra la acción pero "pierde" la respuesta, como un corte justo después de guardar.
    public bool PerderProximaRespuesta { get; set; }
    public HashSet<Guid> EnviosConflictivos { get; } = [];
    public int Llamadas { get; private set; }
    public int Registradas => _procesadas.Count;
    public int Duplicadas { get; private set; }
    public Func<Task>? AlRecibir { get; set; }
    public bool SesionVencida { get; set; }

    public Task<SesionRepartidor?> IniciarSesionAsync(string email, string contrasena, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public Task<HojaDeRutaDto> ObtenerHojaDeRutaAsync(SesionRepartidor sesion, CancellationToken ct = default) =>
        throw new NotSupportedException();

    public async Task<RespuestaAccion> EnviarAccionAsync(SesionRepartidor sesion, AccionEntregaDto accion, CancellationToken ct = default)
    {
        Llamadas++;
        if (AlRecibir is not null) await AlRecibir();
        if (!EnLinea) throw new HttpRequestException("Sin red");
        if (SesionVencida) throw new SesionExpiradaException();
        if (EnviosConflictivos.Contains(accion.EnvioId))
            return new RespuestaAccion(ResultadoAccion.Rechazada, "El envío cambió de estado.");
        if (!_procesadas.Add(accion.IdOperacion))
        {
            Duplicadas++;
            return new RespuestaAccion(ResultadoAccion.YaProcesada);
        }

        if (PerderProximaRespuesta)
        {
            PerderProximaRespuesta = false;
            throw new HttpRequestException("Se cortó antes de recibir la respuesta");
        }

        return new RespuestaAccion(ResultadoAccion.Aceptada);
    }
}

internal static class Datos
{
    public static readonly DateTimeOffset Inicio = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    public static readonly SesionRepartidor Sesion = new("t", Guid.NewGuid(), "repartidor@demo.uy", DateTimeOffset.MaxValue);
    public static readonly GeoPuntoDto Posicion = new(-34.6m, -58.4m);
    public static readonly MotivoNoEntregaDto Ausente = new(Guid.NewGuid(), "Ausente", false);
    public static readonly MotivoNoEntregaDto Rechazo = new(Guid.NewGuid(), "Rechazado", true);

    public static HojaDeRutaDto Hoja(params EstadoEnvio[] estados) => new(Guid.NewGuid(), Inicio,
        estados.Select((e, i) => new ParadaDto(Guid.NewGuid(), i + 1, $"Destinatario {i + 1}", "Calle 1",
            Inicio, Inicio.AddHours(2), e)).ToList(),
        [Ausente, Rechazo],
        [TipoPrueba.Nombre, TipoPrueba.Documento]);

    public static AccionEntregaDto Entrega(Guid envioId, DateTimeOffset? cuando = null, GeoPuntoDto? posicion = null)
    {
        var hora = cuando ?? Inicio;
        var pos = posicion ?? Posicion;
        return new AccionEntregaDto(Guid.NewGuid(), envioId, ResultadoIntento.Entregado, null,
            [new PruebaDto(TipoPrueba.Nombre, "Ana", hora, pos), new PruebaDto(TipoPrueba.Documento, "30111222", hora, pos)],
            hora, pos);
    }

    public static AccionEntregaDto Fallo(Guid envioId, Guid? motivoId, params PruebaDto[] pruebas) =>
        new(Guid.NewGuid(), envioId, ResultadoIntento.Fallido, motivoId, pruebas, Inicio, null);
}
