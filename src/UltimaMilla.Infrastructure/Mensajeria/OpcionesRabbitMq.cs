namespace UltimaMilla.Infrastructure.Mensajeria;

// Se enlaza con la sección "RabbitMq". La clave nunca va en el código ni en appsettings.json:
// se entrega por variable de entorno (RabbitMq__Clave), user-secrets o el gestor de secretos del entorno.
public sealed class OpcionesRabbitMq
{
    public const string Seccion = "RabbitMq";

    public string Host { get; set; } = "127.0.0.1";
    public int Puerto { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Usuario { get; set; } = "";
    public string Clave { get; set; } = "";

    public string Exchange { get; set; } = "ultimamilla.eventos";
    public string ColaAvisos { get; set; } = "avisos";
    // Qué routing keys (Tipo del mensaje) llegan a la cola de avisos.
    public string ClaveEnlaceAvisos { get; set; } = "envio.#";

    // Mensajes sin confirmar que el broker entrega a la vez al consumidor.
    public ushort Prefetch { get; set; } = 10;
    // Al apagar, cuánto se espera a que terminen los mensajes en curso antes de cancelarlos.
    public TimeSpan EsperaCierre { get; set; } = TimeSpan.FromSeconds(20);

    public TimeSpan TiempoMaximoConexion { get; set; } = TimeSpan.FromSeconds(10);

    public string ColaMuertos => ColaAvisos + ".dlq";
    public string ColaEspera(string sufijo) => $"{ColaAvisos}.espera.{sufijo}";

    // Reintentos escalonados: 5 segundos, 30 segundos y 2 minutos.
    public IReadOnlyList<(string Sufijo, TimeSpan Espera)> Esperas { get; } =
    [
        ("5s", TimeSpan.FromSeconds(5)),
        ("30s", TimeSpan.FromSeconds(30)),
        ("2m", TimeSpan.FromMinutes(2)),
    ];

    public void Validar()
    {
        Requerido(Host, nameof(Host));
        Requerido(Usuario, nameof(Usuario));
        Requerido(Clave, nameof(Clave));
        Requerido(VirtualHost, nameof(VirtualHost));
        Requerido(Exchange, nameof(Exchange));
        Requerido(ColaAvisos, nameof(ColaAvisos));
        Requerido(ClaveEnlaceAvisos, nameof(ClaveEnlaceAvisos));
        if (Prefetch < 1) throw new InvalidOperationException("RabbitMq:Prefetch debe ser al menos 1.");
        if (EsperaCierre < TimeSpan.Zero) throw new InvalidOperationException("RabbitMq:EsperaCierre no puede ser negativa.");
        if (Puerto is < 1 or > 65535) throw new InvalidOperationException("RabbitMq:Puerto debe estar entre 1 y 65535.");
        if (TiempoMaximoConexion <= TimeSpan.Zero)
            throw new InvalidOperationException("RabbitMq:TiempoMaximoConexion debe ser positivo.");
    }

    private static void Requerido(string valor, string nombre)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException($"Falta la configuración RabbitMq:{nombre}.");
    }
}
