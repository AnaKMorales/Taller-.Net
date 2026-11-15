using UltimaMilla.Application.Deposito;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Deposito;
using UltimaMilla.Web.Desarrollo;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task DatosDemoCierrePermitenDevolverYCompletarRendicionSinCambiarOtrosOperadores()
    {
        var previo = Escenario();
        await using (var db = Context()) { db.Add(previo.Envio); await db.SaveChangesAsync(); }
        var demo = await DatosDemo.CrearCierreAsync(Options(), default);
        await using var lectura = Context(demo.Usuarios.OperadorId);
        var admin = await lectura.Set<Usuario>().SingleAsync(u => u.Email == demo.Usuarios.EmailAdministrador);
        var repo = new CierreDepositoRepository(Options(), new TenantActual(
            TenantContext.ParaUsuario(demo.Usuarios.OperadorId, admin.Id, RolUsuario.Administrador)));
        var devoluciones = new DevolucionDepositoHandler(repo, TimeProvider.System);
        var rendiciones = new RendirRutaHandler(repo, TimeProvider.System);
        var ruta = await rendiciones.ConsultarAsync(demo.RutaId);
        Assert.Equal(0, ruta.ParadasPendientes);
        Assert.Null(ruta.RendidaEn);
        var envio = await devoluciones.ConsultarAsync(demo.EnvioId);
        Assert.Null(envio.Devolucion);
        envio = await devoluciones.IniciarAsync(demo.EnvioId, new(envio.Version));
        Assert.Equal(demo.Bultos.Order(), envio.Devolucion!.Bultos.Select(b => b.Codigo).Order());
        foreach (var codigo in demo.Bultos)
            envio = (await devoluciones.EscanearAsync(demo.EnvioId, new(codigo, envio.Version))).Envio;
        envio = await devoluciones.CompletarAsync(demo.EnvioId, new(envio.Version));
        Assert.Equal(EstadoDevolucion.Completado, envio.Devolucion!.Estado);
        Assert.NotNull((await rendiciones.Handle(demo.RutaId, new(ruta.Version))).RendidaEn);
        await using var original = Context(previo.Operador.Id);
        Assert.Equal(EstadoEnvio.Admitido, (await original.Envios.SingleAsync()).Estado);
    }
}
