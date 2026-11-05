namespace UltimaMilla.Worker;

public sealed class OpcionesPublicacionPeriodica
{
    public const string Seccion = "PublicacionOutbox";

    public TimeSpan Intervalo { get; set; } = TimeSpan.FromSeconds(2);
    public int MaximoMensajesPorLote { get; set; } = 100;
    // Operadores procesados a la vez. Cada uno usa su propio contexto; no comparten nada.
    public int OperadoresEnParalelo { get; set; } = 4;

    public void Validar()
    {
        if (Intervalo < TimeSpan.FromMilliseconds(10) || Intervalo > TimeSpan.FromHours(1))
            throw new InvalidOperationException("PublicacionOutbox:Intervalo debe estar entre 10 ms y 1 hora.");
        if (MaximoMensajesPorLote is < 1 or > 1000)
            throw new InvalidOperationException("PublicacionOutbox:MaximoMensajesPorLote debe estar entre 1 y 1000.");
        if (OperadoresEnParalelo is < 1 or > 32)
            throw new InvalidOperationException("PublicacionOutbox:OperadoresEnParalelo debe estar entre 1 y 32.");
    }
}
