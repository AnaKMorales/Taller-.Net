using Microsoft.JSInterop;

namespace UltimaMilla.Repartidor.Dispositivo;

// Estado de red según el navegador (eventos online/offline).
// "En línea" significa que hay red, no que el servidor responda: eso lo confirma cada envío.
public sealed class EstadoConexion(Navegador navegador) : IDisposable
{
    private DotNetObjectReference<EstadoConexion>? _receptor;

    public bool EnLinea { get; private set; } = true;

    public event Action? Cambio;

    public async Task IniciarAsync()
    {
        if (_receptor is not null) return;
        _receptor = DotNetObjectReference.Create(this);
        EnLinea = await navegador.EscucharConexionAsync(_receptor);
        Cambio?.Invoke();
    }

    [JSInvokable]
    public void CambioConexion(bool enLinea)
    {
        EnLinea = enLinea;
        Cambio?.Invoke();
    }

    public void Dispose() => _receptor?.Dispose();
}
