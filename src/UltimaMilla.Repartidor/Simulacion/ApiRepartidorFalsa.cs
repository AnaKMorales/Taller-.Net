using System.Text.Json;
using System.Text.Json.Serialization;
using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Dispositivo;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Simulacion;

// DATOS DE PRUEBA para hoja de ruta e intentos: Web todavía no tiene esos endpoints (el login sí es real,
// ver Servidor/ApiRepartidorHttp). Imita lo que hará el servidor real:
// - sin red lanza HttpRequestException, como HttpClient;
// - descarta un IdOperacion repetido (como Envio.RegistrarIntento) y cuenta los duplicados;
// - rechaza intentos sobre envíos que ya no están en tránsito (conflicto).
// Su "base de datos" se guarda en el almacén local para que la demostración sobreviva a una recarga.
public sealed class ApiRepartidorFalsa(IAlmacenLocal almacen, EstadoConexion conexion)
{
    private const string Clave = "servidor-falso";
    private static readonly TimeSpan Latencia = TimeSpan.FromMilliseconds(400);

    private static readonly Guid RutaId = Guid.Parse("9b1d3c1e-0000-4000-8000-000000000001");
    private static readonly Guid MotivoAusente = Guid.Parse("9b1d3c1e-0000-4000-8000-0000000000a1");
    private static readonly Guid MotivoDireccion = Guid.Parse("9b1d3c1e-0000-4000-8000-0000000000a2");
    private static readonly Guid MotivoRechazo = Guid.Parse("9b1d3c1e-0000-4000-8000-0000000000a3");

    // Esta parada la "cambia el despachador" después de la primera descarga, para demostrar un conflicto.
    public static readonly Guid EnvioQueCambiaEnServidor = Guid.Parse("9b1d3c1e-0000-4000-8000-000000000e04");

    private static readonly (Guid Envio, string Destinatario, string Direccion, int Desde, int Hasta)[] Paradas =
    [
        (Guid.Parse("9b1d3c1e-0000-4000-8000-000000000e01"), "Lucía Fernández", "Av. Rivadavia 4520, CABA", 9, 11),
        (Guid.Parse("9b1d3c1e-0000-4000-8000-000000000e02"), "Martín Gómez", "Acoyte 210, CABA", 10, 12),
        (Guid.Parse("9b1d3c1e-0000-4000-8000-000000000e03"), "Sofía Pereyra", "Av. Pedro Goyena 1150, CABA", 12, 14),
        (EnvioQueCambiaEnServidor, "Diego Romero", "Emilio Mitre 830, CABA", 14, 16)
    ];

    public async Task<HojaDeRutaDto> ObtenerHojaDeRutaAsync(CancellationToken ct = default)
    {
        await SimularRedAsync(ct);
        var estado = await LeerEstadoAsync();
        var hoy = DateTimeOffset.Now.Date;
        var offset = DateTimeOffset.Now.Offset;
        var paradas = Paradas.Select((p, i) => new ParadaDto(p.Envio, i + 1, p.Destinatario, p.Direccion,
                new DateTimeOffset(hoy.AddHours(p.Desde), offset), new DateTimeOffset(hoy.AddHours(p.Hasta), offset),
                estado.Estados.GetValueOrDefault(p.Envio, EstadoEnvio.EnTransito)))
            .ToList();

        if (!estado.Estados.ContainsKey(EnvioQueCambiaEnServidor))
        {
            estado.Estados[EnvioQueCambiaEnServidor] = EstadoEnvio.Reprogramado;
            await GuardarEstadoAsync(estado);
        }

        return new HojaDeRutaDto(RutaId, DateTimeOffset.UtcNow, paradas,
        [
            new MotivoNoEntregaDto(MotivoAusente, "Destinatario ausente", false),
            new MotivoNoEntregaDto(MotivoDireccion, "Dirección inexistente", false),
            new MotivoNoEntregaDto(MotivoRechazo, "Rechazado por el destinatario", true)
        ],
        [TipoPrueba.Nombre, TipoPrueba.Documento]);
    }

    public async Task<RespuestaAccion> EnviarAccionAsync(AccionEntregaDto accion, CancellationToken ct = default)
    {
        await SimularRedAsync(ct);
        var estado = await LeerEstadoAsync();

        if (estado.Procesadas.Contains(accion.IdOperacion))
        {
            estado.DuplicadasDescartadas++;
            await GuardarEstadoAsync(estado);
            return new RespuestaAccion(ResultadoAccion.YaProcesada);
        }

        var actual = estado.Estados.GetValueOrDefault(accion.EnvioId, EstadoEnvio.EnTransito);
        if (actual != EstadoEnvio.EnTransito)
            return new RespuestaAccion(ResultadoAccion.Rechazada,
                $"En el servidor el envío está {actual}; el despachador lo modificó. No se registró el intento.");

        estado.Procesadas.Add(accion.IdOperacion);
        estado.Estados[accion.EnvioId] = accion.Resultado == ResultadoIntento.Entregado ? EstadoEnvio.Entregado : EstadoEnvio.NoEntregado;
        await GuardarEstadoAsync(estado);
        return new RespuestaAccion(ResultadoAccion.Aceptada);
    }

    // Para la pantalla de demostración.
    public async Task<(int Registradas, int DuplicadasDescartadas)> ResumenAsync()
    {
        var estado = await LeerEstadoAsync();
        return (estado.Procesadas.Count, estado.DuplicadasDescartadas);
    }

    public Task ReiniciarAsync() => almacen.BorrarAsync(Clave);

    private async Task SimularRedAsync(CancellationToken ct)
    {
        await Task.Delay(Latencia, ct);
        if (!conexion.EnLinea) throw new HttpRequestException("Sin conexión (simulada).");
    }

    private async Task<EstadoServidorFalso> LeerEstadoAsync() =>
        await almacen.LeerAsync(Clave) is { } json
            ? JsonSerializer.Deserialize(json, JsonServidorFalso.Default.EstadoServidorFalso) ?? new()
            : new();

    private Task GuardarEstadoAsync(EstadoServidorFalso estado) =>
        almacen.GuardarAsync(Clave, JsonSerializer.Serialize(estado, JsonServidorFalso.Default.EstadoServidorFalso));
}

public sealed class EstadoServidorFalso
{
    public Dictionary<Guid, EstadoEnvio> Estados { get; set; } = [];
    public List<Guid> Procesadas { get; set; } = [];
    public int DuplicadasDescartadas { get; set; }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(EstadoServidorFalso))]
internal sealed partial class JsonServidorFalso : JsonSerializerContext;
