using NetArchTest.Rules;
using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Web.Components;

namespace UltimaMilla.Architecture.Tests;

public sealed class DependenciasTests
{
    [Fact]
    public void DomainSoloDependeDeSiMismoYBibliotecasDelSistema()
    {
        var result = Types.InAssembly(typeof(Envio).Assembly).Should()
            .OnlyHaveDependenciesOn("System", "UltimaMilla.Domain").GetResult();
        Cumple(result, "Domain debe permanecer independiente de las otras capas y bibliotecas de infraestructura.");
    }

    [Fact]
    public void ApplicationSoloDependeDeDomainYBibliotecasDelSistema()
    {
        var result = Types.InAssembly(typeof(CrearEnvioHandler).Assembly).Should()
            .OnlyHaveDependenciesOn("System", "UltimaMilla.Application", "UltimaMilla.Domain").GetResult();
        Cumple(result, "Application no debe utilizar EF, Redis, RabbitMQ ni implementaciones de Infrastructure o presentación.");
    }

    [Theory]
    [InlineData("Infrastructure")]
    [InlineData("Web")]
    public void LasCapasNoDependenDeOtrasAplicacionesEjecutables(string capa)
    {
        var assembly = capa == "Infrastructure" ? typeof(AppDbContext).Assembly : typeof(App).Assembly;
        var prohibidas = capa == "Infrastructure"
            ? new[] { "UltimaMilla.Web", "UltimaMilla.Worker", "UltimaMilla.Repartidor" }
            : new[] { "UltimaMilla.Worker", "UltimaMilla.Repartidor" };
        Cumple(Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(prohibidas).GetResult(),
            $"{capa} no puede depender de estos proyectos: {string.Join(", ", prohibidas)}.");
    }

    private static void Cumple(TestResult result, string regla) =>
        Assert.True(result.IsSuccessful, $"{regla}{Environment.NewLine}Tipos que incumplen: " +
            string.Join(", ", result.FailingTypeNames ?? []));
}
