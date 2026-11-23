namespace UltimaMilla.Infrastructure.Persistence.Outbox;

public sealed class OpcionesPublicacionOutbox
{
    public TimeSpan TiempoMaximoConfirmacion { get; init; } = TimeSpan.FromSeconds(15);
    public int MaximoIntentosPorMensaje { get; init; } = 3;

    internal void Validar()
    {
        if (MaximoIntentosPorMensaje is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(MaximoIntentosPorMensaje),
                "El límite de intentos por mensaje debe estar entre 1 y 100.");
        if (TiempoMaximoConfirmacion <= TimeSpan.Zero || TiempoMaximoConfirmacion > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(TiempoMaximoConfirmacion),
                "La espera de confirmación debe ser positiva y no superar dos minutos.");
    }
}
