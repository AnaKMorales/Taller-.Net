using System.Net;
using Microsoft.EntityFrameworkCore.Diagnostics;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Operacion;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task CierreDiarioConservaPrimerCierreYActualizaIndicadoresTardiosSinCerrarRutas()
    {
        var (e, _) = await PrepararCierreDeposito(intento: false);
        var reloj = new RelojConfiguracion { Ahora = EscenarioPersistencia.Fecha.AddDays(1) };
        var proceso = new ProcesamientoOperativo(Options(), reloj);
        Assert.Contains(e.Operador.Id, await proceso.ListarOperadoresAsync());
        var primero = await proceso.ProcesarAsync(e.Operador.Id, 31);
        Assert.Equal((2, 1, 0), (primero.Actualizados, primero.Cerrados, primero.Omitidos));
        var consulta = new ConsultaCierresOperativos(Options(), TenantFlota(e));
        var dia = DateOnly.FromDateTime(EscenarioPersistencia.Fecha);
        var cerrado = Assert.Single(await consulta.ConsultarAsync(dia, dia));
        Assert.Equal(1, cerrado.AlCierre!.EnviosAdmitidos.Resumen.Envios);
        Assert.Equal(0, cerrado.AlCierre.EnviosAdmitidos.Resumen.Entregados);
        Assert.Equal(1, cerrado.Actual.RutasSinRendir);
        var hoy = CalendarioOperativo.Hoy(reloj);
        Assert.Null(Assert.Single(await consulta.ConsultarAsync(hoy, hoy)).CerradoEn);

        await using (var db = Context(e.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            var fecha = EscenarioPersistencia.Fecha.AddDays(1).AddHours(1);
            envio.RegistrarIntento(ResultadoIntento.Entregado, null, [e.Foto(fecha)],
                await db.Set<Repartidor>().SingleAsync(), fecha, Guid.NewGuid());
            await db.SaveChangesAsync();
        }
        reloj.Ahora = reloj.Ahora.AddHours(2);
        Assert.Equal(0, (await proceso.ProcesarAsync(e.Operador.Id, 31)).Cerrados);
        var actualizado = Assert.Single(await consulta.ConsultarAsync(dia, dia));
        Assert.Equal(cerrado.CerradoEn, actualizado.CerradoEn);
        Assert.Equal(0, actualizado.AlCierre!.EnviosAdmitidos.Resumen.Entregados);
        Assert.Equal(1, actualizado.Actual.EnviosAdmitidos.Resumen.Entregados);
        Assert.Equal(2, actualizado.Recalculos);
        await using var lectura = Context(e.Operador.Id);
        Assert.Empty(await lectura.Set<Rendicion>().ToListAsync());
        Assert.Empty(await lectura.Set<Devolucion>().ToListAsync());
        Assert.Equal(2, await lectura.Set<ResumenOperativoDiario>().CountAsync());
    }

    [PostgresFact]
    public async Task CierreDiarioApiAislaOperadoresYNoAdmiteCuentaComercialNiAnonimo()
    {
        var e = await PrepararAdministracionFlota();
        var ajeno = Escenario();
        await using (var db = Context(ajeno.Operador.Id)) { db.Add(ajeno.Envio); await db.SaveChangesAsync(); }
        var reloj = new RelojConfiguracion { Ahora = EscenarioPersistencia.Fecha.AddDays(1) };
        var proceso = new ProcesamientoOperativo(Options(), reloj);
        await proceso.ProcesarAsync(e.Operador.Id, 31);
        var consulta = new ConsultaCierresOperativos(Options(), new TenantActual(TenantContext.ParaUsuario(ajeno.Operador.Id, ajeno.Admin.Id, RolUsuario.Administrador)));
        var dia = DateOnly.FromDateTime(EscenarioPersistencia.Fecha);
        Assert.Empty(await consulta.ConsultarAsync(dia, dia));
        using var servidor = new ServidorApi(_testConnection);
        using var admin = servidor.Cliente(e, e.Admin);
        using var repartidor = servidor.Cliente(e);
        using var anonimo = servidor.Cliente();
        var url = $"/api/operacion/cierres?desde={dia:yyyy-MM-dd}&hasta={dia:yyyy-MM-dd}";
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await repartidor.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(url)).StatusCode);
        await using var comercio = ContextoComercio(e, e.Envio.CuentaComercialId);
        Assert.Empty(await comercio.Set<ResumenOperativoDiario>().ToListAsync());
        await using var sinTenant = new AppDbContext(Options());
        Assert.Empty(await sinTenant.Set<ResumenOperativoDiario>().ToListAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/operacion/cierres?desde={dia:yyyy-MM-dd}&hasta={dia.AddDays(-1):yyyy-MM-dd}")).StatusCode);
    }

    [PostgresFact]
    public async Task CierreDiarioOmiteBloqueoDeOtroWorkerYRecuperaLaFechaSinDuplicarCierre()
    {
        var e = await PrepararAdministracionFlota();
        var reloj = new RelojConfiguracion { Ahora = EscenarioPersistencia.Fecha.AddDays(1) };
        var dia = CalendarioOperativo.Hoy(reloj);
        var proceso = new ProcesamientoOperativo(Options(), reloj);
        await using (var db = Context(e.Operador.Id))
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var clave = $"cierre-operativo:{e.Operador.Id:N}:{dia:yyyy-MM-dd}";
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({clave}, 0))");
            var resultado = await proceso.ProcesarAsync(e.Operador.Id, 31);
            Assert.Equal((1, 1), (resultado.Omitidos, resultado.Cerrados));
            await tx.RollbackAsync();
        }
        Assert.Equal(0, (await proceso.ProcesarAsync(e.Operador.Id, 31)).Cerrados);
        await using var lectura = Context(e.Operador.Id);
        Assert.Equal(2, await lectura.Set<ResumenOperativoDiario>().CountAsync());
    }

    private sealed class FalloResumen : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Fallo de resumen");
    }

    [PostgresFact]
    public async Task CierreDiarioFalloNoGuardaCierreParcialYNuevaEjecucionRecupera()
    {
        var e = await PrepararAdministracionFlota();
        var reloj = new RelojConfiguracion { Ahora = EscenarioPersistencia.Fecha.AddDays(1) };
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_testConnection).AddInterceptors(new FalloResumen()).Options;
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProcesamientoOperativo(opciones, reloj).ProcesarAsync(e.Operador.Id, 31));
        await using (var db = Context(e.Operador.Id))
        {
            Assert.Empty(await db.Set<ResumenOperativoDiario>().ToListAsync());
            Assert.Equal(EstadoEnvio.Admitido, (await db.Envios.SingleAsync()).Estado);
        }
        Assert.Equal(1, (await new ProcesamientoOperativo(Options(), reloj).ProcesarAsync(e.Operador.Id, 31)).Cerrados);
    }
}
