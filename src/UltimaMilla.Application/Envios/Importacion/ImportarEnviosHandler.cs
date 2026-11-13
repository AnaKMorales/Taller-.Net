using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.AltaPortal;

namespace UltimaMilla.Application.Envios.Importacion;

public sealed record FilaImportacion(string Referencia, SolicitudAltaPortal Solicitud, string ClaveEntrega);
public enum EstadoFilaImportacion { Creado, Existente, Conflicto, Error }
public sealed record ResultadoFilaImportacion(string Referencia, EstadoFilaImportacion Estado,
    AltaCreada? Envio = null, string? Error = null);

// Cada llamada abre su propia unidad de trabajo: una fila inválida no contamina las siguientes.
public interface IImportacionEnvios
{
    Task<ResultadoFilaImportacion> ImportarFilaAsync(FilaImportacion fila, CancellationToken ct = default);
}

public sealed class ImportarEnviosHandler(IImportacionEnvios repositorio)
{
    public async Task<IReadOnlyList<ResultadoFilaImportacion>> Handle(IReadOnlyList<FilaImportacion> filas,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filas);
        if (filas.Count is 0 or > 500) throw new ArgumentException("El lote debe contener entre 1 y 500 filas.");
        var resultados = new List<ResultadoFilaImportacion>();
        foreach (var fila in filas)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                ArgumentNullException.ThrowIfNull(fila);
                if (string.IsNullOrWhiteSpace(fila.Referencia) || fila.Referencia.Trim().Length > 100)
                    throw new ArgumentException("La referencia debe tener entre 1 y 100 caracteres.");
                ArgumentNullException.ThrowIfNull(fila.Solicitud);
                resultados.Add(await repositorio.ImportarFilaAsync(fila with { Referencia = fila.Referencia.Trim() }, ct));
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException &&
                error is not AislamientoOperadorException)
            {
                resultados.Add(new(fila?.Referencia ?? "", EstadoFilaImportacion.Error, Error: error.Message));
            }
        }
        return resultados;
    }
}
