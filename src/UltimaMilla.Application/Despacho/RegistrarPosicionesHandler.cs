namespace UltimaMilla.Application.Despacho;

// Lectura del GPS que la PWA toma periódicamente durante la jornada (req. 23). IdLectura la genera el dispositivo:
// reenviar un lote tras perder la conexión no duplica filas. La hora es la del dispositivo, en UTC.
public sealed record LecturaPosicionDto(Guid IdLectura, Guid RutaId, decimal Latitud, decimal Longitud,
    decimal? PrecisionMetros, DateTime RegistradaEn);

// Aceptadas: nuevas guardadas. Repetidas: ya estaban (reenvío). Rechazadas: no cumplen las reglas (ruta ajena o sin
// despachar, hora fuera de rango...). Repetidas y rechazadas no se vuelven a mandar: el dispositivo las descarta.
public sealed record PosicionesRegistradasDto(int Aceptadas, int Repetidas, int Rechazadas);

// Puerto de persistencia: toma repartidor y operador del contexto autenticado, nunca del lote.
public interface IRegistroPosiciones
{
    Task<PosicionesRegistradasDto> RegistrarAsync(IReadOnlyList<LecturaPosicionDto> lecturas, CancellationToken ct);
}

public sealed class RegistrarPosicionesHandler(IRegistroPosiciones registro)
{
    public const int MaximoPorLote = 200;

    public Task<PosicionesRegistradasDto> Handle(IReadOnlyList<LecturaPosicionDto>? lecturas, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (lecturas is null || lecturas.Count == 0) throw new ArgumentException("El lote no tiene lecturas.");
        if (lecturas.Count > MaximoPorLote)
            throw new ArgumentException($"El lote admite hasta {MaximoPorLote} lecturas.");
        if (lecturas.Any(l => l is null || l.IdLectura == Guid.Empty || l.RutaId == Guid.Empty))
            throw new ArgumentException("Cada lectura necesita su identificador y el de la ruta.");
        if (lecturas.Select(l => l.IdLectura).Distinct().Count() != lecturas.Count)
            throw new ArgumentException("El lote repite una lectura.");

        // Las horas llegan en UTC desde JSON; una sin zona se interpreta como UTC (nunca la hora local del host).
        var normalizadas = lecturas.Select(l => l with
        {
            RegistradaEn = l.RegistradaEn.Kind switch
            {
                DateTimeKind.Utc => l.RegistradaEn,
                DateTimeKind.Local => l.RegistradaEn.ToUniversalTime(),
                _ => DateTime.SpecifyKind(l.RegistradaEn, DateTimeKind.Utc)
            }
        }).ToArray();
        return registro.RegistrarAsync(normalizadas, ct);
    }
}
