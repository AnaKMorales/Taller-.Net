using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Envios.Importacion;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tests;

public sealed class ImportarEnviosTests
{
    private static FilaImportacion Fila(string referencia) => new(referencia,
        new(Guid.NewGuid(), ModalidadServicio.Estandar, "Persona", "099123456", "p@example.com",
            "Calle", "1", "Montevideo", []), "fecha");

    [Fact]
    public async Task NormalizaReferenciasConservaOrdenYContinuaTrasErroresDeValidacion()
    {
        var repo = new Repositorio();
        var resultados = await new ImportarEnviosHandler(repo).Handle([Fila(" A "), Fila(""), Fila("mala"), Fila("B")]);
        Assert.Equal(["A", "mala", "B"], repo.Referencias);
        Assert.Equal([EstadoFilaImportacion.Creado, EstadoFilaImportacion.Error, EstadoFilaImportacion.Error,
            EstadoFilaImportacion.Creado], resultados.Select(x => x.Estado));
    }

    [Fact]
    public async Task LimitesCancelacionYAislamientoNoSeConviertenEnErroresDeFila()
    {
        var handler = new ImportarEnviosHandler(new Repositorio());
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle([]));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(Enumerable.Repeat(Fila("A"), 501).ToArray()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.Handle([Fila("A")], new CancellationToken(true)));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => handler.Handle([Fila("ajena")]));
    }

    private sealed class Repositorio : IImportacionEnvios
    {
        public List<string> Referencias { get; } = [];
        public Task<ResultadoFilaImportacion> ImportarFilaAsync(FilaImportacion fila, CancellationToken ct = default)
        {
            Referencias.Add(fila.Referencia);
            if (fila.Referencia == "mala") throw new ArgumentException("Fila inválida");
            if (fila.Referencia == "ajena") throw new AislamientoOperadorException();
            return Task.FromResult(new ResultadoFilaImportacion(fila.Referencia, EstadoFilaImportacion.Creado));
        }
    }
}
