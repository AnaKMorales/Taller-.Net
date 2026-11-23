namespace UltimaMilla.Application.Mensajeria;

public interface IPublicadorOutbox
{
    // Una ejecución acotada. Persiste cada confirmación y cada fallo atribuible a un mensaje.
    // Si el mensaje entra en cuarentena, el lote continúa. Los demás errores se propagan.
    Task<int> PublicarLoteAsync(int maximoMensajes = 100, CancellationToken cancellationToken = default);
}
