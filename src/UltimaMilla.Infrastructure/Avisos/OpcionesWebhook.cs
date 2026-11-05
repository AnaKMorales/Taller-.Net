namespace UltimaMilla.Infrastructure.Avisos;

public sealed class OpcionesWebhook
{
    public const string Seccion = "Webhooks";

    // Corto a propósito: el reintento lo hace la cola de espera, no este proceso.
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
    // Solo para desarrollo/pruebas. En producción un comercio no puede apuntar a la red interna.
    public bool PermitirDestinosPrivados { get; set; }

    public void Validar()
    {
        if (Timeout < TimeSpan.FromMilliseconds(100) || Timeout > TimeSpan.FromSeconds(30))
            throw new InvalidOperationException("Webhooks:Timeout debe estar entre 100 ms y 30 s.");
    }
}
