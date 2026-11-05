using RabbitMQ.Client;

namespace UltimaMilla.Infrastructure.Mensajeria;

// Una conexión por proceso, compartida por el transporte y la declaración de topología.
// Los canales no se comparten entre hilos: cada uso crea el suyo.
public sealed class ConexionRabbitMq(OpcionesRabbitMq opciones) : IAsyncDisposable
{
    private readonly SemaphoreSlim _candado = new(1, 1);
    private IConnection? _conexion;

    public async Task<IConnection> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        if (_conexion is { IsOpen: true }) return _conexion;
        await _candado.WaitAsync(cancellationToken);
        try
        {
            if (_conexion is { IsOpen: true }) return _conexion;
            if (_conexion is not null) await _conexion.DisposeAsync();
            opciones.Validar();
            var fabrica = new ConnectionFactory
            {
                HostName = opciones.Host,
                Port = opciones.Puerto,
                VirtualHost = opciones.VirtualHost,
                UserName = opciones.Usuario,
                Password = opciones.Clave,
                ClientProvidedName = "ultimamilla",
                RequestedConnectionTimeout = opciones.TiempoMaximoConexion,
                AutomaticRecoveryEnabled = true,
            };
            _conexion = await fabrica.CreateConnectionAsync(cancellationToken);
            return _conexion;
        }
        finally { _candado.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conexion is not null) await _conexion.DisposeAsync();
        _candado.Dispose();
    }
}
