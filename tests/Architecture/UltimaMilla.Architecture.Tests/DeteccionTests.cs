using NetArchTest.Rules;
using UltimaMilla.Architecture.Tests.Fixtures;

namespace UltimaMilla.Architecture.Tests;

public sealed class DeteccionTests
{
    [Fact]
    public void DetectaUnaDependenciaProhibidaDeEfEnUnaPropiedad()
    {
        var result = Types.InAssembly(typeof(CasoDeUsoConEf).Assembly).That()
            .HaveName(nameof(CasoDeUsoConEf)).ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore").GetResult();
        Assert.False(result.IsSuccessful);
        Assert.Contains(typeof(CasoDeUsoConEf).FullName!, result.FailingTypeNames);
    }

    [Fact]
    public void PermiteUnaDependenciaDeDomainSinInfraestructura()
    {
        var types = Types.InAssembly(typeof(CasoDeUsoSinEf).Assembly).That().HaveName(nameof(CasoDeUsoSinEf));
        Assert.Single(types.GetTypes());
        Assert.True(types.ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore").GetResult().IsSuccessful);
    }
}
