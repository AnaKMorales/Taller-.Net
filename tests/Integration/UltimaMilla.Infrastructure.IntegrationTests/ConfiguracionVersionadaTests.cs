using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Configuracion;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Configuracion;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    private sealed class RelojConfiguracion : TimeProvider
    {
        internal DateTime Ahora = EscenarioPersistencia.Fecha;
        public override DateTimeOffset GetUtcNow() => new(Ahora);
    }
    private sealed class InvalidacionConfiguracion(Func<Guid, Task>? accion = null) : IInvalidacionTarifario
    {
        internal List<Guid> Operadores = [];
        public async Task InvalidarAsync(Guid operadorId, CancellationToken cancellationToken = default)
        {
            Operadores.Add(operadorId);
            if (accion is not null) await accion(operadorId);
        }
    }
    private ConfiguracionVersionada Gestion(EscenarioPersistencia e, RelojConfiguracion? reloj = null,
        InvalidacionConfiguracion? cache = null, TenantContext? tenant = null) => new(Options(),
        new TenantActual(tenant ?? TenantContext.ParaUsuario(e.Operador.Id, e.Admin.Id, RolUsuario.Administrador)),
        reloj ?? new(), cache ?? new(), NullLogger<ConfiguracionVersionada>.Instance);
    private static DatosReglasDto DatosReglas() => new(4, 12, [TipoPrueba.Foto], new(2, 10, true),
        [new(ModalidadServicio.Estandar, 24, new(16, 0)), new(ModalidadServicio.Urgente, 8, new(12, 0))],
        [new("Ausente", "No atiende", Responsable.Destinatario, true, true, true)]);
    private static LineaTarifaDto NuevaLinea(Guid zona) => new(zona, ModalidadServicio.Estandar, 0, 100, 10, 200,
        [new(TipoAjuste.Recargo, 10)]);

    [PostgresFact]
    public async Task PrimerasReglasSeCreanConVersionEsperadaCero()
    {
        var operador = new Operador("Operador sin configuración");
        var admin = new Usuario(operador, "admin-nuevo@example.com", RolUsuario.Administrador);
        _operatorId = operador.Id;
        await using (var db = Context())
        {
            db.Add(admin);
            await db.SaveChangesAsync();
        }
        var gestion = new ConfiguracionVersionada(Options(),
            new TenantActual(TenantContext.ParaUsuario(operador.Id, admin.Id, RolUsuario.Administrador)),
            new RelojConfiguracion(), new InvalidacionConfiguracion(), NullLogger<ConfiguracionVersionada>.Instance);
        Assert.Empty(await gestion.ListarReglasAsync());
        var creada = await gestion.CrearReglasAsync(new(0, null, DatosReglas()));
        Assert.Equal(1, creada.Version.Version);
        Assert.Equal(creada.Version.Id, (await gestion.ReglasVigentesAsync())!.Version.Id);
    }

    [PostgresFact]
    public async Task TarifariosConcurrentesNoDuplicanVersionNiInvalidacion()
    {
        var e = await GuardarEscenario();
        var cache = new InvalidacionConfiguracion();
        // Las llamadas solo llegan a invalidación tras serializarse y confirmar; una sola gana.
        var resultados = await Task.WhenAll(Guardar(), Guardar());
        Assert.Single(resultados, r => r);
        Assert.Single(resultados, r => !r);
        Assert.Single(cache.Operadores);
        Assert.Equal(2, (await Gestion(e).ListarTarifariosAsync()).Count);
        async Task<bool> Guardar()
        {
            try { await Gestion(e, cache: cache).CrearTarifarioAsync(new(1, null, [NuevaLinea(e.Zona.Id)])); return true; }
            catch (ConflictoConcurrenciaException) { return false; }
        }
    }

    [PostgresFact]
    public async Task ReglasVersionadasConsultanHistoriaYVigenciaSinCambiarElEnvio()
    {
        var e = await GuardarEscenario();
        var reloj = new RelojConfiguracion();
        var gestion = Gestion(e, reloj);
        var futura = reloj.Ahora.AddDays(2);
        var result = await gestion.CrearReglasAsync(new(1, futura, DatosReglas()));
        Assert.Equal(2, result.Version.Version);
        Assert.False(result.Version.EnUso);
        Assert.Equal(e.Reglas.Id, (await gestion.ReglasVigentesAsync())!.Version.Id);
        Assert.Equal(new[] { 2, 1 }, (await gestion.ListarReglasAsync()).Select(v => v.Version));
        var detalle = await gestion.ObtenerReglasAsync(result.Version.Id);
        Assert.Equal(4, detalle!.Datos.MaxIntentosEntrega);
        Assert.Equal(2, detalle.Datos.Plazos.Count);
        Assert.Equal("Ausente", Assert.Single(detalle.Datos.Motivos).Nombre);
        reloj.Ahora = futura;
        Assert.Equal(result.Version.Id, (await gestion.ReglasVigentesAsync())!.Version.Id);
        await using var db = Context();
        var envio = await db.Envios.Include(v => v.Reglas).SingleAsync();
        Assert.Equal(e.Reglas.Id, envio.Reglas.Id);
        Assert.Equal(2, envio.Reglas.MaxIntentosEntrega);
    }

    [PostgresFact]
    public async Task TarifarioNuevoInvalidaDespuesDelCommitYConservaPrecioHistorico()
    {
        var e = await GuardarEscenario();
        var cache = new InvalidacionConfiguracion(async op =>
        {
            await using var reader = Context(op);
            Assert.Equal(2, await reader.Set<CuadroTarifario>().CountAsync());
        });
        var gestion = Gestion(e, cache: cache);
        var creada = await gestion.CrearTarifarioAsync(new(1, null, [NuevaLinea(e.Zona.Id)]));
        Assert.False(creada.RevisarInvalidacionCache);
        Assert.Equal(e.Operador.Id, Assert.Single(cache.Operadores));
        Assert.Equal(creada.Version.Id, (await gestion.TarifarioVigenteAsync())!.Version.Id);
        Assert.Equal(2, (await gestion.ListarTarifariosAsync()).Count);
        Assert.Equal(200, Assert.Single((await gestion.ObtenerTarifarioAsync(creada.Version.Id))!.Lineas).Precio);
        await using var reader = Context();
        var envio = await reader.Envios.SingleAsync();
        Assert.Equal(e.Envio.TarifaCalculada, envio.TarifaCalculada);
        Assert.Equal(e.Envio.CuadroTarifarioId, reader.Entry(envio).Property<Guid>("CuadroTarifarioKey").CurrentValue);
    }

    [PostgresFact]
    public async Task DosAdministradoresConLaMismaVersionEsperadaSoloCreanUnaVersion()
    {
        var e = await GuardarEscenario();
        var resultados = await Task.WhenAll(Guardar(), Guardar());
        Assert.Single(resultados, r => r);
        Assert.Single(resultados, r => !r);
        Assert.Equal(2, (await Gestion(e).ListarReglasAsync()).Count);
        async Task<bool> Guardar()
        {
            try { await Gestion(e).CrearReglasAsync(new(1, null, DatosReglas())); return true; }
            catch (ConflictoConcurrenciaException) { return false; }
        }
    }

    [PostgresFact]
    public async Task ConfiguracionSoloAdmiteAdministradoresActivosDelOperador()
    {
        var (a, b) = await DosOperadores();
        var gestion = Gestion(a);
        Assert.Equal(a.Zona.Id, Assert.Single(await gestion.ListarZonasAsync()).Id);
        Assert.Null(await gestion.ObtenerReglasAsync(b.Reglas.Id));
        Assert.Null(await gestion.ObtenerTarifarioAsync(b.Envio.CuadroTarifarioId));
        var falsificado = TenantContext.ParaUsuario(a.Operador.Id, b.Admin.Id, RolUsuario.Administrador);
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => Gestion(a, tenant: falsificado).ListarReglasAsync());
        foreach (var rol in new[] { RolUsuario.Despachador, RolUsuario.OperarioDeposito, RolUsuario.Comercio })
        {
            var tenant = TenantContext.ParaUsuario(a.Operador.Id, a.Admin.Id, rol, rol == RolUsuario.Comercio ? a.Envio.CuentaComercialId : null);
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => Gestion(a, tenant: tenant).ListarTarifariosAsync());
        }
        await using (var db = Context(a.Operador.Id))
        {
            var user = await db.Set<Usuario>().SingleAsync(u => u.Id == a.Admin.Id);
            db.Entry(user).Property(nameof(Usuario.Activo)).CurrentValue = false;
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => gestion.CrearReglasAsync(new(1, null, DatosReglas())));
    }

    [PostgresFact]
    public async Task ZonaAjenaYDatosInvalidosNoDejanVersionesNiInvalidanCache()
    {
        var (a, b) = await DosOperadores();
        var cache = new InvalidacionConfiguracion();
        var gestion = Gestion(a, cache: cache);
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => gestion.CrearTarifarioAsync(new(1, null, [NuevaLinea(b.Zona.Id)])));
        await Assert.ThrowsAsync<InvalidOperationException>(() => gestion.CrearTarifarioAsync(new(1, null, [NuevaLinea(a.Zona.Id), NuevaLinea(a.Zona.Id)])));
        var datos = DatosReglas();
        await Assert.ThrowsAsync<InvalidOperationException>(() => gestion.CrearReglasAsync(new(1, null, datos with { Plazos = [datos.Plazos[0], datos.Plazos[0]] })));
        Assert.Single(await gestion.ListarTarifariosAsync());
        Assert.Single(await gestion.ListarReglasAsync());
        Assert.Empty(cache.Operadores);
    }

    [PostgresFact]
    public async Task VigenciaPasadaOLocalSeRechazaSinGuardar()
    {
        var e = await GuardarEscenario();
        var gestion = Gestion(e);
        await Assert.ThrowsAsync<ArgumentException>(() => gestion.CrearReglasAsync(new(1, EscenarioPersistencia.Fecha.AddSeconds(-1), DatosReglas())));
        await Assert.ThrowsAsync<ArgumentException>(() => gestion.CrearReglasAsync(new(1, DateTime.SpecifyKind(EscenarioPersistencia.Fecha, DateTimeKind.Unspecified), DatosReglas())));
        Assert.Single(await gestion.ListarReglasAsync());
    }

    [PostgresFact]
    public async Task ErrorDeInvalidacionNoPresentaComoFallidoUnGuardadoConfirmado()
    {
        var e = await GuardarEscenario();
        var gestion = Gestion(e, cache: new InvalidacionConfiguracion(_ => throw new IOException("Cache de prueba caída")));
        var creada = await gestion.CrearTarifarioAsync(new(1, null, [NuevaLinea(e.Zona.Id)]));
        Assert.True(creada.RevisarInvalidacionCache);
        Assert.NotNull(await gestion.ObtenerTarifarioAsync(creada.Version.Id));
    }

    [PostgresFact]
    public async Task PersistenciaBloqueaCambiosBajasYAjustesAgregadosAVersionesGuardadas()
    {
        var e = await GuardarEscenario();
        foreach (var tipo in new[] { "reglas", "linea", "politica", "ajuste", "baja", "nuevoajuste" })
        {
            await using var db = Context();
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync();
            switch (tipo)
            {
                case "reglas": db.Entry(envio.Reglas).Property(nameof(ReglasOperativas.MaxIntentosEntrega)).CurrentValue = 99; break;
                case "linea": db.Entry(envio.CuadroTarifario.Lineas.Single()).Property(nameof(LineaTarifa.Precio)).CurrentValue = 999m; break;
                case "politica": db.Entry(envio.Reglas.PoliticaDevolucion).Property(nameof(PoliticaDevolucion.DiasEnDeposito)).CurrentValue = 99; break;
                case "ajuste": db.Entry(envio.CuadroTarifario.Lineas.Single().Ajustes.Single()).Property(nameof(AjusteTarifario.ValorPorcentaje)).CurrentValue = 99m; break;
                case "baja": db.Remove(envio.Reglas.MotivosNoEntrega.Single()); break;
                case "nuevoajuste":
                    var fresh = await Gestion(e).CrearTarifarioAsync(new(1, null, [NuevaLinea(e.Zona.Id)]));
                    var cuadro = await db.Set<CuadroTarifario>().Include(c => c.Operador).Include(c => c.Lineas).ThenInclude(l => l.Ajustes).SingleAsync(c => c.Id == fresh.Version.Id);
                    db.Add(new AjusteTarifario(cuadro.Lineas.Single(), TipoAjuste.Recargo, 1));
                    break;
            }
            if (tipo == "linea") Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
            else await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        await using var reader = Context();
        Assert.Equal(2, (await reader.Set<ReglasOperativas>().SingleAsync()).MaxIntentosEntrega);
        Assert.Equal(150, (await reader.Set<LineaTarifa>().Where(l => EF.Property<Guid>(l, "CuadroKey") == e.Envio.CuadroTarifarioId).SingleAsync()).Precio);
    }
}
