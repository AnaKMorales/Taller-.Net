using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Outbox;

namespace UltimaMilla.Infrastructure.Tests;

public sealed class PublicadorOutboxTests
{
    [Fact]
    public async Task SoloProcesoInternoPuedePublicarInclusoParaRolesAdministrativos()
    {
        var op = Guid.NewGuid();
        var account = Guid.NewGuid();
        var contexts = new TenantContext?[] { null,
            TenantContext.ParaUsuario(op, Guid.NewGuid(), RolUsuario.Administrador),
            TenantContext.ParaUsuario(op, Guid.NewGuid(), RolUsuario.Comercio, account),
            TenantContext.ParaMensaje(op), TenantContext.ParaSeguimiento(op, account, Guid.NewGuid()) };
        foreach (var tenant in contexts)
        {
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=no_conectar").Options, tenant is null ? null : new TenantActual(tenant));
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => new PublicadorOutbox(db, new TransporteNoInvocable(),
                TimeProvider.System, new()).PublicarLoteAsync());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task RechazaLotesFueraDeLimiteAntesDeConectar(int cantidad)
    {
        await using var db = new AppDbContextFactory().CreateDbContext([]);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new PublicadorOutbox(db,
            new TransporteNoInvocable(), TimeProvider.System, new()).PublicarLoteAsync(cantidad));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public async Task RechazaEsperaDeConfirmacionInvalidaAntesDeConectar(int segundos)
    {
        await using var db = new AppDbContextFactory().CreateDbContext([]);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new PublicadorOutbox(db,
            new TransporteNoInvocable(), TimeProvider.System,
            new() { TiempoMaximoConfirmacion = TimeSpan.FromSeconds(segundos) }).PublicarLoteAsync());
    }

    private sealed class TransporteNoInvocable : ITransporteMensajes
    {
        public Task PublicarConfirmadoAsync(MensajeParaPublicar mensaje, CancellationToken cancellationToken = default) =>
            throw new Xunit.Sdk.XunitException("No debe invocar al transporte.");
    }
}
