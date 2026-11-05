using System.Text.Json;
using UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Repartidor.Sincronizacion;

// Cola de acciones registradas en el dispositivo, persistida en el almacén local.
// Se guarda completa en una sola clave: son pocas acciones por día y así cada cambio es una sola escritura.
public sealed class ColaAcciones(IAlmacenLocal almacen)
{
    private const string Clave = "cola-acciones";

    // La pantalla y el sincronizador pueden modificar la cola "a la vez" (entre dos await).
    // El semáforo hace que cada leer-modificar-guardar termine antes de que empiece otro.
    private readonly SemaphoreSlim _cerrojo = new(1, 1);

    // Avisa a las pantallas que la cola cambió (indicador de pendientes, lista).
    public event Action? Cambio;

    public async Task<IReadOnlyList<AccionPendiente>> ListarAsync()
    {
        await _cerrojo.WaitAsync();
        try { return await LeerAsync(); }
        finally { _cerrojo.Release(); }
    }

    // Devuelve los errores de validación; lista vacía = encolada.
    public async Task<IReadOnlyList<string>> EncolarAsync(AccionEntregaDto accion, HojaDeRutaDto hoja)
    {
        var errores = ValidadorAccion.Validar(accion, hoja).ToList();
        await _cerrojo.WaitAsync();
        try
        {
            var cola = await LeerAsync();
            if (cola.Any(a => a.Accion.IdOperacion == accion.IdOperacion))
                errores.Add("Esta acción ya está registrada.");
            // Evita dos resultados para el mismo envío (por ejemplo, un doble toque o entregado y fallido).
            if (cola.Any(a => a.Accion.EnvioId == accion.EnvioId && a.Estado != EstadoAccion.Rechazada))
                errores.Add("El envío ya tiene una acción registrada.");
            if (errores.Count > 0) return errores;

            cola.Add(new AccionPendiente(accion));
            await GuardarAsync(cola);
        }
        finally { _cerrojo.Release(); }

        Cambio?.Invoke();
        return errores;
    }

    // Reemplaza una acción por su versión actualizada (la busca por IdOperacion).
    public async Task ActualizarAsync(AccionPendiente actualizada)
    {
        await ModificarAsync(cola =>
        {
            var i = cola.FindIndex(a => a.Accion.IdOperacion == actualizada.Accion.IdOperacion);
            if (i >= 0) cola[i] = actualizada;
        });
    }

    // Solo se descartan las rechazadas: una pendiente todavía no llegó al servidor y se perdería.
    public Task DescartarRechazadaAsync(Guid idOperacion) =>
        ModificarAsync(cola => cola.RemoveAll(a => a.Accion.IdOperacion == idOperacion && a.Estado == EstadoAccion.Rechazada));

    // Solo para reiniciar la demostración con datos de prueba.
    public Task VaciarAsync() => ModificarAsync(cola => cola.Clear());

    private async Task ModificarAsync(Action<List<AccionPendiente>> cambio)
    {
        await _cerrojo.WaitAsync();
        try
        {
            var cola = await LeerAsync();
            cambio(cola);
            await GuardarAsync(cola);
        }
        finally { _cerrojo.Release(); }

        Cambio?.Invoke();
    }

    private async Task<List<AccionPendiente>> LeerAsync() =>
        await almacen.LeerAsync(Clave) is { } json
            ? JsonSerializer.Deserialize(json, JsonRepartidor.Default.ListAccionPendiente) ?? []
            : [];

    private Task GuardarAsync(List<AccionPendiente> cola) =>
        almacen.GuardarAsync(Clave, JsonSerializer.Serialize(cola, JsonRepartidor.Default.ListAccionPendiente));
}
