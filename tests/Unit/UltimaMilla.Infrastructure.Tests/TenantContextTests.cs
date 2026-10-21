using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Tests;

public sealed class TenantContextTests
{
    [Fact]
    public void UsuarioComercioExigeCuentaYUsuarioInternoNoAceptaCuenta()
    {
        var op = Guid.NewGuid();
        var user = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => TenantContext.ParaUsuario(op, user, RolUsuario.Comercio));
        Assert.Throws<ArgumentException>(() => TenantContext.ParaUsuario(op, user, RolUsuario.Administrador, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => TenantContext.ParaMensaje(Guid.Empty));
        Assert.Throws<ArgumentException>(() => TenantContext.ParaMensaje(op, Guid.Empty));
        Assert.Throws<ArgumentException>(() => TenantContext.ParaUsuario(op, Guid.Empty, RolUsuario.Administrador));
    }

    [Fact]
    public void CacheSeparaOperadoresCuentasRolesYSeguimientoPublico()
    {
        var op = Guid.NewGuid();
        var account = Guid.NewGuid();
        var user = Guid.NewGuid();
        var scopes = new[]
        {
            TenantContext.ParaUsuario(op, user, RolUsuario.Comercio, account),
            TenantContext.ParaUsuario(op, Guid.NewGuid(), RolUsuario.Comercio, account),
            TenantContext.ParaUsuario(op, user, RolUsuario.Comercio, Guid.NewGuid()),
            TenantContext.ParaUsuario(Guid.NewGuid(), user, RolUsuario.Comercio, account),
            TenantContext.ParaUsuario(op, user, RolUsuario.Administrador),
            TenantContext.ParaUsuario(op, user, RolUsuario.Despachador),
            TenantContext.ParaMensaje(op, account),
            TenantContext.ParaSeguimiento(op, account, Guid.NewGuid()),
            TenantContext.ParaSeguimiento(op, account, Guid.NewGuid())
        };
        var keys = scopes.Select(c => TenantCacheKey.Crear(c, "config", "1")).ToArray();
        Assert.Equal(scopes.Length, keys.Distinct().Count());
        foreach (var context in scopes) Assert.StartsWith(TenantCacheKey.Prefijo(context), TenantCacheKey.Crear(context, "config", "1"));
        foreach (var context in scopes.Where(c => c.OperadorId == op))
            Assert.StartsWith(TenantCacheKey.PrefijoOperador(op), TenantCacheKey.Crear(context, "config", "1"));
        Assert.NotEqual(TenantCacheKey.Crear(scopes[0], "a:b", "c"), TenantCacheKey.Crear(scopes[0], "a", "b:c"));
    }
}
