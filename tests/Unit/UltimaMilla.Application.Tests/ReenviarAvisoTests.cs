using System.Text.Json;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Portal;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tests;

public sealed class ReenviarAvisoTests
{
    private static readonly DateTime Ahora = new(2026, 11, 8, 15, 0, 0, DateTimeKind.Utc);

    private static readonly EventoEnvioV1 Evento = new(Guid.Empty, EventoEnvioV1.VersionContrato, Guid.NewGuid(),
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "EntregaParcial", "AppRepartidor", "EnTransito",
        "EntregadoParcialmente", Ahora.AddHours(-1), Guid.NewGuid());

    [Fact]
    public async Task UnAvisoFallidoSePublicaConElMismoEventoYUnMensajeNuevo()
    {
        var transporte = new TransporteEspia();
        var resultado = await Handler(new PanelFalso(new AvisoReenviable(false, Evento)), transporte).Handle(Guid.NewGuid());

        Assert.Equal(ResultadoReenvio.Reenviado, resultado);
        var mensaje = Assert.Single(transporte.Publicados);
        Assert.Equal(EventoEnvioV1.TipoMensaje, mensaje.Tipo);
        Assert.Equal(Evento.OperadorId, mensaje.OperadorId);
        Assert.Equal(Ahora, mensaje.CreadoEn);
        Assert.NotEqual(Guid.Empty, mensaje.MensajeId);
        // El cuerpo es el mismo evento V1, con el MensajeId nuevo (si no, el Worker lo descartaría como duplicado).
        var cuerpo = JsonSerializer.Deserialize<EventoEnvioV1>(mensaje.Contenido, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(Evento with { MensajeId = mensaje.MensajeId }, cuerpo);
    }

    [Fact]
    public async Task DosReenviosUsanMensajesDistintos()
    {
        var transporte = new TransporteEspia();
        var handler = Handler(new PanelFalso(new AvisoReenviable(false, Evento)), transporte);
        await handler.Handle(Guid.NewGuid());
        await handler.Handle(Guid.NewGuid());
        Assert.Equal(2, transporte.Publicados.Select(m => m.MensajeId).Distinct().Count());
    }

    [Fact]
    public async Task UnAvisoInexistenteODeOtraCuentaNoPublicaNada()
    {
        var transporte = new TransporteEspia();
        Assert.Equal(ResultadoReenvio.NoEncontrado, await Handler(new PanelFalso(null), transporte).Handle(Guid.NewGuid()));
        Assert.Empty(transporte.Publicados);
    }

    [Fact]
    public async Task UnAvisoYaEntregadoNoSeReenvia()
    {
        var transporte = new TransporteEspia();
        Assert.Equal(ResultadoReenvio.YaEntregado,
            await Handler(new PanelFalso(new AvisoReenviable(true, Evento)), transporte).Handle(Guid.NewGuid()));
        Assert.Empty(transporte.Publicados);
    }

    [Fact]
    public async Task SiLaColaNoEstaSeInformaNoDisponible()
    {
        var transporte = new TransporteEspia { Falla = true };
        Assert.Equal(ResultadoReenvio.NoDisponible,
            await Handler(new PanelFalso(new AvisoReenviable(false, Evento)), transporte).Handle(Guid.NewGuid()));
    }

    private static ReenviarAvisoHandler Handler(IPanelAvisos panel, ITransporteMensajes transporte) =>
        new(panel, transporte, new RelojFijo());

    private sealed class RelojFijo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Ahora);
    }

    private sealed class TransporteEspia : ITransporteMensajes
    {
        public bool Falla { get; init; }
        public List<MensajeParaPublicar> Publicados { get; } = [];

        public Task PublicarConfirmadoAsync(MensajeParaPublicar mensaje, CancellationToken cancellationToken = default)
        {
            if (Falla) throw new InvalidOperationException("broker caído");
            Publicados.Add(mensaje);
            return Task.CompletedTask;
        }
    }

    private sealed class PanelFalso(AvisoReenviable? aviso) : IPanelAvisos
    {
        public Task<AvisoReenviable?> AvisoParaReenviarAsync(Guid avisoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(aviso);
        public Task<IReadOnlyList<SuscripcionDto>> SuscripcionesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<AvisoDto>> UltimosAvisosAsync(int cantidad, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<SuscripcionCreada> CrearSuscripcionAsync(Uri destino, IReadOnlyCollection<TipoEvento> eventos,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> DesactivarSuscripcionAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
