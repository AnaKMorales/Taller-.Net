namespace UltimaMilla.Infrastructure.Persistence.Inbox;

// Registro técnico de idempotencia, no una entidad del negocio. Clave: (MensajeId, Consumidor).
// El operador (TenantId) lo estampa el interceptor de tenant, igual que en MensajesOutbox.
public sealed class MensajeProcesado
{
    private MensajeProcesado() { Consumidor = null!; }

    internal MensajeProcesado(Guid mensajeId, string consumidor, DateTime procesadoEn)
    {
        if (mensajeId == Guid.Empty) throw new ArgumentException("El mensaje es obligatorio.", nameof(mensajeId));
        ArgumentException.ThrowIfNullOrWhiteSpace(consumidor);
        if (procesadoEn.Kind != DateTimeKind.Utc) throw new ArgumentException("La fecha debe ser UTC.", nameof(procesadoEn));
        MensajeId = mensajeId;
        Consumidor = consumidor;
        ProcesadoEn = procesadoEn;
    }

    public Guid MensajeId { get; private set; }
    public string Consumidor { get; private set; }
    public DateTime ProcesadoEn { get; private set; }
}
