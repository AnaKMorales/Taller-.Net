using UltimaMilla.Repartidor.Contratos;

namespace UltimaMilla.Repartidor.Dispositivo;

public enum ResultadoLectura { Nuevo, Repetido, Sobrante, Vacio }

// Control de los bultos escaneados en una visita: cuáles se leyeron, cuáles faltan y qué códigos leídos no son de
// esta visita (sobrantes). Lógica pura, sin JS, para poder probarla. Lo usa Componentes/RegistrarBultos.razor.
public sealed class ControlEscaneo(IReadOnlyList<BultoParadaDto> bultos)
{
    private readonly HashSet<Guid> _escaneados = [];
    private readonly List<string> _sobrantes = [];

    public IReadOnlyList<BultoParadaDto> Escaneados => bultos.Where(b => _escaneados.Contains(b.BultoId)).ToList();
    public IReadOnlyList<BultoParadaDto> Faltantes => bultos.Where(b => !_escaneados.Contains(b.BultoId)).ToList();
    public IReadOnlyList<string> Sobrantes => _sobrantes;
    public int Total => bultos.Count;
    public bool HuboLecturas => _escaneados.Count > 0 || _sobrantes.Count > 0;
    public bool Completo => _escaneados.Count == bultos.Count;

    public (ResultadoLectura Resultado, BultoParadaDto? Bulto) Leer(string? codigo)
    {
        var limpio = codigo?.Trim();
        if (string.IsNullOrEmpty(limpio)) return (ResultadoLectura.Vacio, null);
        var bulto = LecturaCodigo.Buscar(bultos, limpio);
        if (bulto is null)
        {
            // Se anota una sola vez: un lector que repite la lectura no infla la lista.
            if (!_sobrantes.Contains(limpio, StringComparer.Ordinal)) _sobrantes.Add(limpio);
            return (ResultadoLectura.Sobrante, null);
        }
        return (_escaneados.Add(bulto.BultoId) ? ResultadoLectura.Nuevo : ResultadoLectura.Repetido, bulto);
    }
}
