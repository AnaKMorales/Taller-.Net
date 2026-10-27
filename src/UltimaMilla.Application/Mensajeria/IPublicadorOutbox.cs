namespace UltimaMilla.Application.Mensajeria;

public interface IPublicadorOutbox
{
    // Una ejecución acotada, sin bucle en segundo plano ni política de reintentos.
    // Cada mensaje confirmado se guarda individualmente. Un error detiene el lote y se propaga.
    Task<int> PublicarLoteAsync(int maximoMensajes = 100, CancellationToken cancellationToken = default);
}
