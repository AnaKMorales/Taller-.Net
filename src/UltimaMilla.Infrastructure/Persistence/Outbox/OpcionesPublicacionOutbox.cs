namespace UltimaMilla.Infrastructure.Persistence.Outbox;

public sealed class OpcionesPublicacionOutbox
{
    public TimeSpan TiempoMaximoConfirmacion { get; init; } = TimeSpan.FromSeconds(15);

    internal void Validar()
    {
        if (TiempoMaximoConfirmacion <= TimeSpan.Zero || TiempoMaximoConfirmacion > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(TiempoMaximoConfirmacion),
                "La espera de confirmación debe ser positiva y no superar dos minutos.");
    }
}
