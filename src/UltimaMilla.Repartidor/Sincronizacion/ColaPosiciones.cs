using System.Text.Json;
using UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Repartidor.Sincronizacion;

// Lecturas del GPS que todavía no llegaron al servidor, por repartidor, en el almacén local (cifrado en el
// navegador, ver docs/cifrado-pwa.md). Con un tope: si el dispositivo pasa muchas horas sin conexión se descartan
// las más viejas, que para el mapa de la flota valen menos que las recientes.
public sealed class ColaPosiciones(IAlmacenLocal almacen)
{
    public const int Maximo = 720; // 24 h con una lectura cada 2 minutos
    private const string Prefijo = "cola-posiciones";
    private readonly SemaphoreSlim _cerrojo = new(1, 1);

    public async Task AgregarAsync(Guid repartidorId, LecturaPosicionDto lectura)
    {
        await _cerrojo.WaitAsync();
        try
        {
            var lista = await LeerAsync(repartidorId);
            lista.Add(lectura);
            if (lista.Count > Maximo) lista.RemoveRange(0, lista.Count - Maximo);
            await GuardarAsync(repartidorId, lista);
        }
        finally { _cerrojo.Release(); }
    }

    public async Task<IReadOnlyList<LecturaPosicionDto>> PrimerasAsync(Guid repartidorId, int cantidad)
    {
        await _cerrojo.WaitAsync();
        try { return (await LeerAsync(repartidorId)).Take(cantidad).ToList(); }
        finally { _cerrojo.Release(); }
    }

    public async Task QuitarAsync(Guid repartidorId, IEnumerable<Guid> ids)
    {
        var quitar = ids.ToHashSet();
        await _cerrojo.WaitAsync();
        try
        {
            var lista = await LeerAsync(repartidorId);
            if (lista.RemoveAll(l => quitar.Contains(l.IdLectura)) > 0) await GuardarAsync(repartidorId, lista);
        }
        finally { _cerrojo.Release(); }
    }

    private async Task<List<LecturaPosicionDto>> LeerAsync(Guid repartidorId) =>
        await almacen.LeerAsync(Clave(repartidorId)) is { } json
            ? JsonSerializer.Deserialize(json, JsonRepartidor.Default.ListLecturaPosicionDto) ?? []
            : [];

    private Task GuardarAsync(Guid repartidorId, List<LecturaPosicionDto> lista) =>
        almacen.GuardarAsync(Clave(repartidorId), JsonSerializer.Serialize(lista, JsonRepartidor.Default.ListLecturaPosicionDto));

    private static string Clave(Guid repartidorId) => $"{Prefijo}:{repartidorId}";
}
