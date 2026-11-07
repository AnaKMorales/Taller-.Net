using UltimaMilla.Application.Abstractions;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.RegistrarIntento;

public sealed class RegistrarIntentoHandler(IRegistroIntentoRepository repository)
{
    public async Task<IntentoRegistradoDto> Handle(RegistrarIntentoCommand command, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var solicitud = ValidarYCopiar(command);
        try
        {
            return await RegistrarAsync(solicitud, ct);
        }
        catch (ConflictoConcurrenciaException error) when (error is not ConflictoIntentoException)
        {
            // El contexto anterior ya se descartó. Solo podemos reconocer una operación confirmada.
            return await RegistrarAsync(solicitud, ct, conflictoDeGuardado: error);
        }
    }

    private async Task<IntentoRegistradoDto> RegistrarAsync(RegistrarIntentoCommand solicitud, CancellationToken ct,
        ConflictoConcurrenciaException? conflictoDeGuardado = null)
    {
        await using var operacion = await repository.AbrirAsync(solicitud.EnvioId, solicitud.ParadaId, solicitud.IdOperacion, ct);
        var envio = operacion.Envio;
        var repetido = envio.Intentos.Any(i => i.IdOperacion == solicitud.IdOperacion);
        // El Id de operación solo evita comprobar la versión si Domain verifica igual contenido.
        if (!repetido && (conflictoDeGuardado is not null || operacion.VersionActual != solicitud.VersionEsperada))
            throw DescribirConflicto(solicitud, operacion,
                conflictoDeGuardado ?? new InvalidOperationException("La versión esperada del envío ya no está vigente."));

        var pruebas = solicitud.Evidencias.ToDictionary(e => e.Referencia,
            e => new PruebaEntrega(e.Tipo, e.Hora, e.Posicion,
                e.Archivo is null ? null : new ArchivoRef(e.Archivo), e.Texto));
        var resultados = solicitud.Bultos.Select(b =>
        {
            var motivo = b.MotivoId is Guid id ? envio.Reglas.MotivosNoEntrega.SingleOrDefault(m => m.Id == id)
                ?? throw new ArgumentException("El motivo no pertenece al catálogo de reglas del envío.") : null;
            return new ResultadoBultoEntrega(b.BultoId, b.Resultado, motivo, b.Evidencias.Select(id => pruebas[id]).ToArray());
        }).ToArray();
        var intento = envio.RegistrarIntentoPorBulto(solicitud.ParadaId, resultados, operacion.Responsable,
            solicitud.OcurridoEn, solicitud.IdOperacion, solicitud.Posicion);
        if (!repetido) await operacion.GuardarAsync(ct);

        return new(intento.Id, envio.Id, intento.IdOperacion, intento.Resultado, envio.Estado,
            operacion.VersionActual, repetido, intento.Detalles.OrderBy(d => d.Bulto.Id)
                .Select(d => new ResultadoBultoRegistradoDto(d.Bulto.Id, d.Resultado, d.Motivo?.Id)).ToArray());
    }

    private static ConflictoIntentoException DescribirConflicto(RegistrarIntentoCommand solicitud,
        IOperacionRegistroIntento operacion, Exception causa)
    {
        var parada = operacion.ParadaOrigen;
        var activa = parada?.Activa == true && operacion.Envio.Parada?.Id == parada.Id;
        var composicion = parada?.Bultos.ToDictionary(b => b.Bulto.Id, b => b.Bulto) ?? [];
        var bultos = solicitud.Bultos.Select(resultado =>
        {
            if (!composicion.TryGetValue(resultado.BultoId, out var bulto))
                return new BultoEnConflictoDto(resultado.BultoId, null, null, MotivoConflictoBulto.FueraDeParada);

            var motivo = bulto.Estado != EstadoBulto.EnTransito
                ? MotivoConflictoBulto.EstadoCambio
                : activa ? MotivoConflictoBulto.SinCambioDeEstado : MotivoConflictoBulto.ParadaCerrada;
            return new BultoEnConflictoDto(bulto.Id, bulto.Codigo, bulto.Estado, motivo);
        }).ToArray();
        return new(new(solicitud.ParadaId, operacion.VersionActual, activa, bultos), causa);
    }

    private static RegistrarIntentoCommand ValidarYCopiar(RegistrarIntentoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.EnvioId == Guid.Empty || command.ParadaId == Guid.Empty || command.VersionEsperada == Guid.Empty ||
            command.IdOperacion == Guid.Empty || command.OcurridoEn == default || command.OcurridoEn.Kind != DateTimeKind.Utc ||
            command.Bultos is null || command.Bultos.Count == 0 || command.Evidencias is null)
            throw new ArgumentException("Faltan identificadores, versión, resultados o instante UTC del intento.");
        var bultos = command.Bultos.Select(b => b is null || b.Evidencias is null ?
            throw new ArgumentException("Faltan resultados o referencias de evidencia.") :
            b with { Evidencias = b.Evidencias.ToArray() }).ToArray();
        var evidencias = command.Evidencias.ToArray();
        if (bultos.Any(b => b.BultoId == Guid.Empty || b.MotivoId == Guid.Empty ||
                b.Resultado is not (ResultadoIntento.Entregado or ResultadoIntento.Fallido) ||
                b.Evidencias.Distinct().Count() != b.Evidencias.Count) ||
            bultos.Select(b => b.BultoId).Distinct().Count() != bultos.Length ||
            evidencias.Any(e => e is null || e.Referencia == Guid.Empty || e.Hora == default || e.Hora.Kind != DateTimeKind.Utc) ||
            evidencias.Select(e => e.Referencia).Distinct().Count() != evidencias.Length)
            throw new ArgumentException("Los resultados y evidencias deben ser válidos y no repetir identificadores.");
        if (!bultos.SelectMany(b => b.Evidencias).ToHashSet().SetEquals(evidencias.Select(e => e.Referencia)))
            throw new ArgumentException("Las referencias de evidencia deben existir y todas las evidencias deben utilizarse.");
        return command with { Bultos = bultos, Evidencias = evidencias };
    }
}
