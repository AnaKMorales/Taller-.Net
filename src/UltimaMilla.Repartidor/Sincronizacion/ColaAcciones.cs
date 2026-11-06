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

    public async Task<IReadOnlyList<AccionPendiente>> ListarAsync(Guid? repartidorId = null)
    {
        await _cerrojo.WaitAsync();
        try { return await LeerAsync(repartidorId); }
        finally { _cerrojo.Release(); }
    }

    // Devuelve los errores de validación; lista vacía = encolada.
    public async Task<IReadOnlyList<string>> EncolarAsync(AccionEntregaDto accion, HojaDeRutaDto hoja, Guid? repartidorId = null)
    {
        var errores = ValidadorAccion.Validar(accion, hoja).ToList();
        if (accion.Intento is not null && (repartidorId is null || repartidorId == Guid.Empty))
            errores.Add("La visita requiere el identificador del repartidor.");
        await _cerrojo.WaitAsync();
        try
        {
            var cola = await LeerAsync(repartidorId);
            if (cola.Any(a => a.Accion.IdOperacion == accion.IdOperacion))
                errores.Add("Esta acción ya está registrada.");
            if (cola.Any(a => a.Accion.CorrespondeA(accion.EnvioId, accion.Intento?.ParadaId) &&
                              a.Estado != EstadoAccion.Rechazada))
                errores.Add("El envío ya tiene una acción registrada.");
            if (errores.Count > 0) return errores;

            cola.Add(new AccionPendiente(accion));
            await GuardarAsync(repartidorId, cola);
        }
        finally { _cerrojo.Release(); }

        Cambio?.Invoke();
        return errores;
    }

    // Reemplaza una acción por su versión actualizada (la busca por IdOperacion).
    public async Task ActualizarAsync(AccionPendiente actualizada, Guid? repartidorId = null)
    {
        await ModificarAsync(repartidorId, cola =>
        {
            var i = cola.FindIndex(a => a.Accion.IdOperacion == actualizada.Accion.IdOperacion);
            if (i >= 0) cola[i] = actualizada;
        });
    }

    // Solo se descartan las rechazadas: una pendiente todavía no llegó al servidor y se perdería.
    public Task DescartarRechazadaAsync(Guid idOperacion, Guid? repartidorId = null) =>
        ModificarAsync(repartidorId, cola => cola.RemoveAll(a => a.Accion.IdOperacion == idOperacion && a.Estado == EstadoAccion.Rechazada));

    // Solo para reiniciar la demostración con datos de prueba.
    public Task VaciarDemoAsync(Guid repartidorId) =>
        ModificarAsync(repartidorId, cola => cola.RemoveAll(a => a.Accion.Intento is null));

    private async Task ModificarAsync(Guid? repartidorId, Action<List<AccionPendiente>> cambio)
    {
        await _cerrojo.WaitAsync();
        try
        {
            var cola = await LeerAsync(repartidorId);
            cambio(cola);
            await GuardarAsync(repartidorId, cola);
        }
        finally { _cerrojo.Release(); }

        Cambio?.Invoke();
    }

    private async Task<List<AccionPendiente>> LeerAsync(Guid? repartidorId) =>
        await almacen.LeerAsync(ClaveDe(repartidorId)) is { } json
            ? JsonSerializer.Deserialize(json, JsonRepartidor.Default.ListAccionPendiente) ?? []
            : [];

    private Task GuardarAsync(Guid? repartidorId, List<AccionPendiente> cola) =>
        almacen.GuardarAsync(ClaveDe(repartidorId), JsonSerializer.Serialize(cola, JsonRepartidor.Default.ListAccionPendiente));

    private static string ClaveDe(Guid? repartidorId) =>
        repartidorId is { } id ? $"{Clave}:{id}" : Clave;
}
