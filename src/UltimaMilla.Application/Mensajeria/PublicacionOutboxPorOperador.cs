namespace UltimaMilla.Application.Mensajeria;

// Los filtros de tenant impiden leer sin contexto, así que "listar operadores" es una excepción
// deliberada y mínima (igual que ISeguimientoPublico): solo devuelve identificadores.
public interface IEnumeradorOperadoresOutbox
{
    // Operadores con al menos un mensaje del outbox sin publicar. No expone ningún otro dato.
    Task<IReadOnlyList<Guid>> ListarConPendientesAsync(CancellationToken cancellationToken = default);
}

public interface IPublicadorOutboxPorOperador
{
    // Crea un contexto propio (ProcesoInterno) para ese operador, publica un lote y lo descarta.
    // No se conserva nada entre llamadas.
    Task<int> PublicarLoteAsync(Guid operadorId, int maximoMensajes = 100, CancellationToken cancellationToken = default);
}
