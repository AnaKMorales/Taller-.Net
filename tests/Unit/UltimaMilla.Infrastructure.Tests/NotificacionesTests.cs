using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Avisos;
using UltimaMilla.Infrastructure.Notificaciones;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.Tests;

public sealed class NotificacionesTests
{
    private static readonly EventoEnvioV1 Evento = new(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "IniciarEntrega", "Sistema", "AsignadoARuta", "EnTransito", DateTime.UtcNow, null);
    private static readonly TenantContext Tenant = TenantContext.ParaMensaje(Evento.OperadorId, Evento.CuentaComercialId);

    private sealed class Manejador(Exception? error = null) : IManejadorEventoEnvio
    {
        public int Llamadas { get; private set; }
        public Task ManejarAsync(EventoEnvioV1 evento, TenantContext tenant, CancellationToken cancellationToken = default)
        {
            Llamadas++;
            return error is null ? Task.CompletedTask : Task.FromException(error);
        }
    }

    [Fact]
    public async Task ElCompuestoCorreTodosAunqueUnoFalleYRelanzaElPrimerError()
    {
        var falla = new Manejador(new InvalidOperationException("aviso"));
        var siguiente = new Manejador();
        var otraFalla = new Manejador(new TimeoutException("webhook"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ManejadoresEventoEnvio([falla, siguiente, otraFalla]).ManejarAsync(Evento, Tenant));

        Assert.Equal("aviso", error.Message);
        Assert.Equal((1, 1, 1), (falla.Llamadas, siguiente.Llamadas, otraFalla.Llamadas));
    }

    [Fact]
    public async Task LaCancelacionNoSeTragaYCortaElRecorrido()
    {
        using var corte = new CancellationTokenSource();
        await corte.CancelAsync();
        var cancela = new Manejador(new OperationCanceledException(corte.Token));
        var siguiente = new Manejador();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            new ManejadoresEventoEnvio([cancela, siguiente]).ManejarAsync(Evento, Tenant, corte.Token));
        Assert.Equal(0, siguiente.Llamadas);
    }

    [Theory]
    [InlineData("persona@example.com", "p***@example.com")]
    [InlineData("sinarroba", "***")]
    [InlineData("@example.com", "***")]
    public void ElLogNoGuardaElEmailCompleto(string destino, string esperado) =>
        Assert.Equal(esperado, EnviadorNotificacionesRegistro.Enmascarar(destino));

    [Fact]
    public void ElWorkerUsaElCompuestoConAvisoAlDestinatarioYWebhooks()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddSingleton(new DbContextOptionsBuilder<Persistence.AppDbContext>().UseNpgsql("Host=localhost").Options);
        servicios.AgregarConsumoMensajes();
        servicios.AgregarEntregaWebhooks();
        servicios.AgregarNotificacionesDestinatario(new() { UrlPublica = "https://ultimamilla.example" });
        using var proveedor = servicios.BuildServiceProvider();

        Assert.Single(servicios, s => s.ServiceType == typeof(IManejadorEventoEnvio));
        Assert.IsType<ManejadoresEventoEnvio>(proveedor.GetRequiredService<IManejadorEventoEnvio>());
        Assert.Equal("https://ultimamilla.example", proveedor.GetRequiredService<OpcionesNotificaciones>().UrlPublica);
    }
}
