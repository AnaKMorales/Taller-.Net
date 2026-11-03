using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task MigracionDeParcialesConservaReglasYEnviosPreviosDeshabilitados()
    {
        var e = await GuardarEscenario();
        await using (var migration = Context())
        {
            var migrator = migration.GetService<IMigrator>();
            // Simula una base de la versión anterior que ya contiene envíos y reglas en uso.
            await migrator.MigrateAsync("20261030191957_CredencialesLogin");
            await migrator.MigrateAsync();
        }
        await using var db = Context();
        var envio = await db.Envios.Include(x => x.Reglas).SingleAsync();
        Assert.Equal(e.Envio.Id, envio.Id);
        Assert.Equal(e.Reglas.Id, envio.Reglas.Id);
        Assert.True(envio.Reglas.EnUso);
        Assert.False(envio.Reglas.PermiteEntregaParcial);
        Assert.Equal(AccionBultosPendientes.NuevaVisitaSinRecargo, envio.Reglas.AccionPendientes);
        Assert.Equal(e.Envio.TarifaCalculada, envio.TarifaCalculada);
        Assert.Equal(EstadoEnvio.Admitido, envio.Estado);
    }

    [PostgresFact]
    public async Task OpcionesParcialesSeVersionanAislanYProtegenSinCambiarReglasHistoricas()
    {
        var (a, b) = await DosOperadores();
        var gestion = Gestion(a);
        var version = 1;
        foreach (var accion in Enum.GetValues<AccionBultosPendientes>())
        {
            var datos = DatosReglas() with { PermiteEntregaParcial = true, AccionPendientes = accion,
                ImporteRecargoNuevaVisita = accion == AccionBultosPendientes.NuevaVisitaConRecargo ? 25 : null };
            var nueva = await gestion.CrearReglasAsync(new(version++, null, datos));
            var detalle = await gestion.ObtenerReglasAsync(nueva.Version.Id);
            Assert.True(detalle!.Datos.PermiteEntregaParcial);
            Assert.Equal(accion, detalle.Datos.AccionPendientes);
            Assert.Equal(datos.ImporteRecargoNuevaVisita, detalle.Datos.ImporteRecargoNuevaVisita);
            Assert.Equal(nueva.Version.Id, (await gestion.ReglasVigentesAsync())!.Version.Id);
            Assert.Null(await Gestion(b).ObtenerReglasAsync(nueva.Version.Id));

            await using var writer = Context(a.Operador.Id);
            var reglas = await writer.Set<ReglasOperativas>().SingleAsync(x => x.Id == nueva.Version.Id);
            writer.Entry(reglas).Property(x => x.PermiteEntregaParcial).CurrentValue = false;
            await Assert.ThrowsAsync<InvalidOperationException>(() => writer.SaveChangesAsync());
        }
        await using var reader = Context(a.Operador.Id);
        var envio = await reader.Envios.Include(x => x.Reglas).SingleAsync();
        Assert.Equal(a.Reglas.Id, envio.Reglas.Id);
        Assert.False(envio.Reglas.PermiteEntregaParcial);
        Assert.False((await Gestion(b).ReglasVigentesAsync())!.Datos.PermiteEntregaParcial);
    }

    [PostgresFact]
    public async Task PoliticaParcialInvalidaNoCreaVersionYPostgresTambienLaRechaza()
    {
        var e = await GuardarEscenario();
        var gestion = Gestion(e);
        var datos = DatosReglas() with { PermiteEntregaParcial = true, AccionPendientes = (AccionBultosPendientes)999 };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => gestion.CrearReglasAsync(new(1, null, datos)));
        Assert.Single(await gestion.ListarReglasAsync());
        await using var db = Context();
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"ReglasOperativas\" SET \"AccionPendientes\" = 999 WHERE \"Id\" = {e.Reglas.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("CK_ReglasOperativas_AccionPendientes", error.ConstraintName);
    }
}
