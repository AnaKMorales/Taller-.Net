using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.CrearEnvio;

namespace UltimaMilla.Infrastructure.Tests;

public sealed class IntegracionCapasTests
{
    [Fact]
    public void ApplicationNoReferenciaInfraestructuraNiEfEInfrastructureConoceApplication()
    {
        var applicationReferences = typeof(CrearEnvioHandler).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        Assert.Contains("UltimaMilla.Domain", applicationReferences);
        Assert.DoesNotContain("UltimaMilla.Infrastructure", applicationReferences);
        Assert.DoesNotContain(applicationReferences, name => name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.Contains(typeof(AppDbContext).Assembly.GetReferencedAssemblies(), a => a.Name == "UltimaMilla.Application");
    }

    [Fact]
    public async Task FormularioInicialIncompletoNoIntentaPersistirUnEnvioInvalido()
    {
        var handler = new CrearEnvioHandler(new RepositoryNoInvocable(), TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new CrearEnvioCommand
        {
            DestinatarioNombre = "Persona", DireccionCalle = "Calle"
        }));
    }

    [Fact]
    public async Task AltaSinUsuarioAutenticadoSeRechazaAntesDeConectar()
    {
        using var db = new AppDbContextFactory().CreateDbContext([]);
        await Assert.ThrowsAsync<AislamientoOperadorException>(() =>
            new AltaEnvioRepository(db).CargarAsync(new CrearEnvioCommand(), CancellationToken.None));
    }

    private sealed class RepositoryNoInvocable : IAltaEnvioRepository
    {
        public Task<ReferenciasAltaEnvio> CargarAsync(CrearEnvioCommand command, CancellationToken cancellationToken) =>
            throw new Xunit.Sdk.XunitException("Un alta incompleta no debe consultar la base.");
        public Task GuardarAsync(Envio envio, CancellationToken cancellationToken) =>
            throw new Xunit.Sdk.XunitException("Un alta incompleta no debe guardar datos.");
    }
}
