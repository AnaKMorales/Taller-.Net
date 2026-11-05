using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Infrastructure.Mensajeria;

namespace UltimaMilla.Infrastructure.Tests;

public sealed class OpcionesRabbitMqTests
{
    [Fact]
    public void SinUsuarioOClaveNoValida()
    {
        Assert.Throws<InvalidOperationException>(() => new OpcionesRabbitMq { Clave = "x" }.Validar());
        Assert.Throws<InvalidOperationException>(() => new OpcionesRabbitMq { Usuario = "u" }.Validar());
    }

    [Fact]
    public void NombresPorDefectoYEsperasSegunElContrato()
    {
        var o = new OpcionesRabbitMq { Usuario = "u", Clave = "c" };
        o.Validar();
        Assert.Equal("ultimamilla.eventos", o.Exchange);
        Assert.Equal("avisos", o.ColaAvisos);
        Assert.Equal("avisos.dlq", o.ColaMuertos);
        Assert.Equal(["avisos.espera.5s", "avisos.espera.30s", "avisos.espera.2m"],
            o.Esperas.Select(e => o.ColaEspera(e.Sufijo)));
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)],
            o.Esperas.Select(e => e.Espera));
    }
}

public sealed class ColaFallidosRegistroTests
{
    private static ServiceProvider Construir(Dictionary<string, string?> valores)
    {
        var configuracion = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .Add(new Microsoft.Extensions.Configuration.Memory.MemoryConfigurationSource { InitialData = valores }).Build();
        return new Microsoft.Extensions.DependencyInjection.ServiceCollection().AgregarColaFallidos(configuracion).BuildServiceProvider();
    }

    [Fact]
    public async Task ConUsuarioYClaveSeUsaLaColaReal()
    {
        await using var sp = Construir(new() { ["RabbitMq:Usuario"] = "u", ["RabbitMq:Clave"] = "c", ["RabbitMq:Puerto"] = "55672" });
        var cola = sp.GetRequiredService<UltimaMilla.Application.Mensajeria.IColaFallidos>();
        Assert.True(cola.Configurada);
        Assert.IsType<ColaFallidosRabbitMq>(cola);
    }

    [Fact]
    public async Task SinClaveLaPantallaRecibeUnaColaNoConfiguradaEnVezDeFallarAlArrancar()
    {
        await using var sp = Construir(new() { ["RabbitMq:Usuario"] = "u" });
        Assert.False(sp.GetRequiredService<UltimaMilla.Application.Mensajeria.IColaFallidos>().Configurada);
    }
}
