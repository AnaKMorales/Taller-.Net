using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Tests;

public sealed class TenancyTests
{
    [Fact]
    public void LasClavesSeparanOperadoresCuentasUsuariosYEnviosPublicos()
    {
        var operador = Guid.NewGuid();
        var cuenta = Guid.NewGuid();
        var usuario = Guid.NewGuid();
        var envio = Guid.NewGuid();
        var contextos = new[]
        {
            TenantContext.ParaUsuario(operador, usuario, RolUsuario.Comercio, cuenta),
            TenantContext.ParaUsuario(operador, usuario, RolUsuario.Comercio, Guid.NewGuid()),
            TenantContext.ParaUsuario(operador, Guid.NewGuid(), RolUsuario.Comercio, cuenta),
            TenantContext.ParaUsuario(Guid.NewGuid(), usuario, RolUsuario.Comercio, cuenta),
            TenantContext.ParaSeguimiento(operador, cuenta, envio),
            TenantContext.ParaSeguimiento(operador, cuenta, Guid.NewGuid()),
            TenantContext.ParaMensaje(operador, cuenta),
            TenantActual.ParaOperador(operador).Contexto
        };

        var claves = contextos.Select(c => TenantCacheKey.Crear(c, "seguimiento", "recurso")).ToArray();
        Assert.Equal(claves.Length, claves.Distinct().Count());
        Assert.Equal(OrigenTenant.ProcesoInterno, contextos[^1].Origen);
        Assert.Equal(envio, contextos[4].EnvioPublicoId);
        Assert.Equal(usuario, contextos[0].UsuarioId);
    }

    [Fact]
    public void LosSeparadoresEnLosNombresNoGeneranColisiones()
    {
        var contexto = TenantContext.ParaProcesoInterno(Guid.NewGuid());
        Assert.NotEqual(TenantCacheKey.Crear(contexto, "seguimiento:estado", "actual"),
            TenantCacheKey.Crear(contexto, "seguimiento", "estado:actual"));
    }

    [Fact]
    public void RechazaPerfilesSinUnAmbitoValido()
    {
        var operador = Guid.NewGuid();
        var usuario = Guid.NewGuid();
        var cuenta = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => TenantContext.ParaUsuario(operador, usuario, RolUsuario.Comercio));
        Assert.Throws<ArgumentException>(() => TenantContext.ParaUsuario(operador, usuario, RolUsuario.Administrador, cuenta));
        Assert.Throws<ArgumentException>(() => TenantContext.ParaUsuario(operador, Guid.Empty, RolUsuario.Administrador));
        Assert.Throws<ArgumentException>(() => TenantContext.ParaMensaje(Guid.Empty));
        Assert.Throws<ArgumentException>(() => TenantContext.ParaSeguimiento(operador, cuenta, Guid.Empty));
        Assert.Throws<ArgumentException>(() => TenantCacheKey.PrefijoOperador(Guid.Empty));
    }
}
