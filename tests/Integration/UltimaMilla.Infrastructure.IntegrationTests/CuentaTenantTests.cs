using System.Security.Claims;
using System.Text.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task DosComerciosDelMismoOperadorNoVenLosDatosDelOtro()
    {
        var (a, b, ua, ub) = await DosCuentas();
        await CompletarFlujo(a);
        await CompletarFlujo(a, b.Id);
        foreach (var pair in new[] { (Envio: a.Envio, User: ua), (Envio: b, User: ub) })
        {
            var tenant = await Resolver().DesdeClaimsAsync(Claims(pair.Envio, pair.User));
            await using var db = Scoped(tenant);
            Assert.Equal(pair.Envio.Id, (await db.Envios.SingleAsync()).Id);
            var other = pair.Envio.Id == b.Id ? a.Envio : b;
            Assert.Null(await db.Envios.FindAsync(other.Id));
            Assert.Null(await db.Set<Destinatario>().FindAsync(other.DestinatarioId));
            Assert.Null(await db.Set<Bulto>().FindAsync(other.Bultos.Single().Id));
            Assert.Single(await db.Set<Bulto>().ToListAsync());
            Assert.Single(await db.Set<Destinatario>().ToListAsync());
            Assert.Single(await db.Set<Direccion>().ToListAsync());
            Assert.Single(await db.Set<IntentoEntrega>().ToListAsync());
            Assert.Single(await db.Set<PruebaEntrega>().ToListAsync());
            Assert.Single(await db.Set<Devolucion>().ToListAsync());
            Assert.Single(await db.Set<Discrepancia>().ToListAsync());
            Assert.Single(await db.Set<SuscripcionAviso>().ToListAsync());
            Assert.Single(await db.Set<EntregaAviso>().ToListAsync());
            Assert.Single(await db.Set<Notificacion>().ToListAsync());
            Assert.Single(await db.Set<Parada>().ToListAsync());
            Assert.Equal(2, await db.Set<EscaneoBulto>().CountAsync());
            Assert.Single(await db.Set<ProgramacionEntrega>().ToListAsync());
            Assert.All(await db.Set<EventoDeEnvio>().Select(e => EF.Property<Guid>(e, "EnvioKey")).ToListAsync(), id => Assert.Equal(pair.Envio.Id, id));
            Assert.Equal(pair.Envio.CuentaComercialId, (await db.Set<CuentaComercial>().SingleAsync()).Id);
            Assert.Single(await db.Set<Comercio>().ToListAsync());
            Assert.Equal(pair.User.Id, (await db.Set<Usuario>().SingleAsync()).Id);
            Assert.Single(await db.Set<ReglasOperativas>().ToListAsync());
            Assert.Empty(await db.Set<Ruta>().ToListAsync());
            Assert.Empty(await db.Set<Vehiculo>().ToListAsync());
            Assert.Empty(await db.Set<Rendicion>().ToListAsync());
            Assert.Empty(await db.Set<Repartidor>().ToListAsync());
            Assert.Single(await db.Set<Dictionary<string, object>>("UsuariosCuentasComerciales").ToListAsync());
        }
        await using var internalDb = Context(a.Operador.Id);
        Assert.Equal(2, await internalDb.Envios.CountAsync());
        Assert.Equal(2, await internalDb.Set<CuentaComercial>().CountAsync());
    }

    [PostgresFact]
    public async Task CuentaRechazaAltasCambiosYBajasAjenosAunqueElOperadorSeaElMismo()
    {
        var (a, b, ua, _) = await DosCuentas();
        var tenant = await Resolver().DesdeClaimsAsync(Claims(a.Envio, ua));
        await using (var db = Scoped(tenant))
        {
            var foreign = new Bulto("Ajeno", 1, 10, 10, 10);
            db.Add(foreign);
            db.Entry(foreign).Property("EnvioKey").CurrentValue = b.Id;
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        foreach (var delete in new[] { false, true })
        {
            await using var db = Scoped(tenant);
            var stub = new Bulto("Objeto desconectado", 1, 10, 10, 10);
            db.Entry(stub).Property("Id").CurrentValue = b.Bultos.Single().Id;
            db.Entry(stub).Property("TenantId").CurrentValue = a.Operador.Id;
            db.Attach(stub);
            if (delete) db.Remove(stub); else db.Entry(stub).Property(nameof(Bulto.Peso)).CurrentValue = 999m;
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        foreach (var replacement in new[] { "CuentaComercialKey", "DestinatarioKey", "ProgramacionKey" })
        {
            await using var db = Scoped(tenant);
            var own = await db.Envios.SingleAsync();
            db.Entry(own).Property(replacement).CurrentValue = replacement switch
            {
                "CuentaComercialKey" => b.CuentaComercialId,
                "DestinatarioKey" => b.DestinatarioId,
                _ => await ContextSnapshotId(b.Id)
            };
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        await using (var db = Scoped(tenant))
        {
            var rules = await db.Set<ReglasOperativas>().SingleAsync();
            db.Entry(rules).Property(nameof(ReglasOperativas.MaxIntentosEntrega)).CurrentValue = 99;
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
        }
        await using var reader = Context(a.Operador.Id);
        Assert.Equal(2, await reader.Envios.CountAsync());
        Assert.Equal(3m, await reader.Set<Bulto>().Where(p => EF.Property<Guid>(p, "EnvioKey") == b.Id).Select(p => p.Peso).SingleAsync());
    }

    [PostgresFact]
    public async Task ComercioPuedeCrearSuEnvioYCongelarConfiguracionNueva()
    {
        var (a, _, ua, _) = await DosCuentas();
        Guid rulesId, cuadroId;
        await using (var db = Context(a.Operador.Id))
        {
            var op = await db.Set<Operador>().SingleAsync();
            var zona = await db.Set<ZonaCobertura>().SingleAsync();
            var rules = new ReglasOperativas(op, 2, EscenarioPersistencia.Fecha.AddDays(-1), 2, 0, [TipoPrueba.Foto], new PoliticaDevolucion(1, 5, true));
            _ = new PlazoServicio(rules, ModalidadServicio.Estandar, 24, new(16, 0));
            var cuadro = new CuadroTarifario(op, 2, EscenarioPersistencia.Fecha.AddDays(-1));
            _ = new LineaTarifa(cuadro, zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
            db.Add(rules); db.Add(cuadro);
            await db.SaveChangesAsync();
            rulesId = rules.Id; cuadroId = cuadro.Id;
        }
        var tenant = await Resolver().DesdeClaimsAsync(Claims(a.Envio, ua));
        Guid newId;
        await using (var db = Scoped(tenant))
        {
            var account = await db.Set<CuentaComercial>().Include(c => c.Operador).SingleAsync();
            var user = await db.Set<Usuario>().Include(u => u.CuentasComerciales).SingleAsync();
            var rules = await db.Set<ReglasOperativas>().Include(r => r.PlazosServicio).Include(r => r.PoliticaDevolucion).SingleAsync(r => r.Id == rulesId);
            var cuadro = await db.Set<CuadroTarifario>().Include(c => c.Lineas).SingleAsync(c => c.Id == cuadroId);
            var zona = await db.Set<ZonaCobertura>().Include(z => z.Franjas).SingleAsync();
            var envio = new Envio(account, new Destinatario("Nuevo", "099123123", "nuevo@example.com", new Direccion("Calle", "1", "Montevideo", "11000")),
                [new Bulto("NUEVO", 1, 10, 10, 10)], ModalidadServicio.Estandar, zona, rules, cuadro, 150,
                EscenarioPersistencia.Fecha, EscenarioPersistencia.Fecha.AddHours(1),
                ProgramacionEntrega.Crear(zona.Franjas.Single(), new(2026, 10, 14), TimeSpan.FromHours(-3)), user);
            db.Add(envio);
            await db.SaveChangesAsync();
            newId = envio.Id;
        }
        await using var reader = Scoped(tenant);
        Assert.Equal(2, await reader.Envios.CountAsync());
        Assert.Equal(a.Envio.CuentaComercialId, await reader.Envios.Where(e => e.Id == newId).Select(e => EF.Property<Guid>(e, "CuentaComercialKey")).SingleAsync());
        Assert.True(await reader.Set<ReglasOperativas>().Where(r => r.Id == rulesId).Select(r => r.EnUso).SingleAsync());
    }

    [PostgresFact]
    public async Task ClaimsValidanCuentaRolEstadoYNoAceptanSuplantaciones()
    {
        var (a, b, ua, _) = await DosCuentas();
        var resolver = Resolver();
        var byCommerce = new ClaimsPrincipal(new ClaimsIdentity([
            new(ClaimTypes.NameIdentifier, ua.Id.ToString()), new("operador_id", a.Operador.Id.ToString()),
            new(ClaimTypes.Role, "Comercio"), new("comercio_id", a.Envio.ComercioId.ToString())], "test"));
        Assert.Equal(a.Envio.CuentaComercialId, (await resolver.DesdeClaimsAsync(byCommerce)).CuentaComercialId);
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => resolver.DesdeClaimsAsync(new ClaimsPrincipal(new ClaimsIdentity(Claims(a.Envio, ua).Claims))));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => resolver.DesdeClaimsAsync(Claims(b, ua)));
        var falseRole = Claims(a.Envio, ua);
        ((ClaimsIdentity)falseRole.Identity!).RemoveClaim(falseRole.FindFirst(ClaimTypes.Role)!);
        ((ClaimsIdentity)falseRole.Identity!).AddClaim(new(ClaimTypes.Role, "Administrador"));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => resolver.DesdeClaimsAsync(falseRole));
        var conflict = Claims(a.Envio, ua);
        ((ClaimsIdentity)conflict.Identity!).AddClaim(new("operador_id", Guid.NewGuid().ToString()));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => resolver.DesdeClaimsAsync(conflict));
        await using (var db = Context(a.Operador.Id))
        {
            var user = await db.Set<Usuario>().SingleAsync(u => u.Id == ua.Id);
            user.Desactivar(); await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => resolver.DesdeClaimsAsync(Claims(a.Envio, ua)));
    }

    [PostgresFact]
    public async Task RevocarLaCuentaImpideResolverYGuardarConElContextoAnterior()
    {
        var (a, _, ua, _) = await DosCuentas();
        var tenant = await Resolver().DesdeClaimsAsync(Claims(a.Envio, ua));
        await using (var db = Context(a.Operador.Id))
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"UsuariosCuentasComerciales\" WHERE \"UsuarioId\" = {ua.Id}");
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => Resolver().DesdeClaimsAsync(Claims(a.Envio, ua)));
        await using var stale = Scoped(tenant);
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => stale.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task MensajesWorkerCreanAmbitosIndependientesYValidanSuCuenta()
    {
        var (a, b) = await DosOperadores();
        var resolver = Resolver();
        var first = await resolver.DesdeMensajeAsync(new(a.Operador.Id, a.Envio.CuentaComercialId));
        var second = await resolver.DesdeMensajeAsync(new(b.Operador.Id, b.Envio.CuentaComercialId));
        var results = await Task.WhenAll(Read(first), Read(second), Read(first));
        Assert.Equal([a.Envio.Id, b.Envio.Id, a.Envio.Id], results);
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => resolver.DesdeMensajeAsync(new(a.Operador.Id, b.Envio.CuentaComercialId)));
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => resolver.DesdeMensajeAsync(new(Guid.Empty)));
        async Task<Guid> Read(TenantContext context) { await using var db = Scoped(context); return await db.Envios.Select(e => e.Id).SingleAsync(); }
    }

    [PostgresFact]
    public async Task SeguimientoResuelveElTokenYDevuelveSoloUnDtoPublicoDeSoloLectura()
    {
        var (a, b, _, _) = await DosCuentas();
        await CompletarFlujo(a, b.Id);
        var service = new SeguimientoPublico(Options());
        var own = await service.ConsultarAsync(a.Envio.CodigoSeguimiento);
        var other = await service.ConsultarAsync(b.CodigoSeguimiento);
        Assert.Equal(EstadoEnvio.Admitido, own!.Estado);
        Assert.Equal(EstadoEnvio.Devuelto, other!.Estado);
        Assert.Equal(TipoEvento.Admitir, own.Historial.First().Tipo);
        Assert.Contains(other.Historial, h => h.Tipo == TipoEvento.DevolucionExitosa);
        Assert.True(own.Bultos.Total > 0);
        Assert.Equal(0, own.Bultos.Entregados);
        Assert.Null(await service.ConsultarAsync(Guid.NewGuid().ToString("N")));
        Assert.Null(await service.ConsultarAsync("invalid token"));
        var json = JsonSerializer.Serialize(own);
        Assert.DoesNotContain("Id", json);
        Assert.DoesNotContain("@", json);
        Assert.DoesNotContain(a.Envio.Id.ToString(), json);
        var publicContext = TenantContext.ParaSeguimiento(a.Operador.Id, a.Envio.CuentaComercialId, a.Envio.Id);
        await using var db = Scoped(publicContext);
        Assert.Equal(a.Envio.Id, (await db.Envios.SingleAsync()).Id);
        Assert.Empty(await db.Set<Usuario>().ToListAsync());
        Assert.Empty(await db.Set<CuentaComercial>().ToListAsync());
        Assert.Empty(await db.Set<Bulto>().ToListAsync());
        await Assert.ThrowsAsync<AislamientoOperadorException>(() => db.SaveChangesAsync());
    }

    [PostgresFact]
    public async Task UsuarioConDosCuentasSigueLimitadoALaCuentaActiva()
    {
        var (a, b, ua, _) = await DosCuentas();
        await using (var db = Context(a.Operador.Id))
        {
            var user = await db.Set<Usuario>().Include(u => u.Operador).Include(u => u.CuentasComerciales).SingleAsync(u => u.Id == ua.Id);
            var second = await db.Set<CuentaComercial>().Include(c => c.Operador).SingleAsync(c => c.Id == b.CuentaComercialId);
            user.AsignarCuentaComercial(second); await db.SaveChangesAsync();
        }
        var first = await Resolver().DesdeClaimsAsync(Claims(a.Envio, ua));
        var secondContext = await Resolver().DesdeClaimsAsync(Claims(b, ua));
        await using var firstDb = Scoped(first);
        await using var secondDb = Scoped(secondContext);
        Assert.Equal(a.Envio.Id, (await firstDb.Envios.SingleAsync()).Id);
        Assert.Equal(b.Id, (await secondDb.Envios.SingleAsync()).Id);
    }

    [PostgresFact]
    public async Task WorkerPuedeModificarYEliminarDatosDeSuCuentaSinAfectarLaOtra()
    {
        var (a, b, _, _) = await DosCuentas();
        await CompletarFlujo(a); await CompletarFlujo(a, b.Id);
        var tenant = await Resolver().DesdeMensajeAsync(new(a.Operador.Id, a.Envio.CuentaComercialId));
        await using (var db = Scoped(tenant))
        {
            var delivery = await db.Set<EntregaAviso>().Include(d => d.Evento).SingleAsync();
            delivery.RegistrarIntento(true, EscenarioPersistencia.Fecha.AddHours(3));
            db.Remove(await db.Set<Notificacion>().SingleAsync());
            await db.SaveChangesAsync();
        }
        await using var reader = Context(a.Operador.Id);
        Assert.Single(await reader.Set<Notificacion>().ToListAsync());
        Assert.True(await reader.Set<EntregaAviso>().Where(d => EF.Property<Guid>(d.Suscripcion, "CuentaComercialKey") == a.Envio.CuentaComercialId)
            .Select(d => d.Exitoso).SingleAsync());
        Assert.False(await reader.Set<EntregaAviso>().Where(d => EF.Property<Guid>(d.Suscripcion, "CuentaComercialKey") == b.CuentaComercialId)
            .Select(d => d.Exitoso).SingleAsync());
    }

    [PostgresFact]
    public async Task ProgramacionHistoricaTambienSeLimitaALaCuenta()
    {
        var (a, _, ua, _) = await DosCuentas();
        await using (var db = Context(a.Operador.Id))
        {
            var envio = await EscenarioPersistencia.Grafo(db).SingleAsync(e => e.Id == a.Envio.Id);
            var admin = await db.Set<Usuario>().SingleAsync(u => u.Id == a.Admin.Id);
            var driver = await db.Set<Repartidor>().SingleAsync();
            var vehicle = await db.Set<Vehiculo>().SingleAsync();
            EscaneoBulto.RegistrarRecepcion(envio.Bultos.Single(), admin, EscenarioPersistencia.Fecha);
            envio.RecibirEnDeposito(admin, EscenarioPersistencia.Fecha);
            var ruta = new Ruta(driver, vehicle, 10);
            ruta.AgregarParada(envio, 1, EscenarioPersistencia.Fecha.AddHours(1), admin, EscenarioPersistencia.Fecha);
            db.Add(ruta); await db.SaveChangesAsync();
            envio.Aplazar(ProgramacionEntrega.Crear(envio.Zona.Franjas.Single(), new(2026, 10, 15), TimeSpan.FromHours(-3)), admin, EscenarioPersistencia.Fecha);
            await db.SaveChangesAsync();
        }
        var tenant = await Resolver().DesdeClaimsAsync(Claims(a.Envio, ua));
        await using var reader = Scoped(tenant);
        Assert.Equal(2, await reader.Set<ProgramacionEntrega>().CountAsync());
        var previous = await reader.Set<EventoDeEnvio>().Where(e => e.Tipo == TipoEvento.Aplazar).Select(e => e.ProgramacionAnterior!.FechaEntrega).SingleAsync();
        Assert.Equal(new DateOnly(2026, 10, 14), previous);
    }

    private DbContextOptions<AppDbContext> Options() => new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_testConnection).Options;
    private AppDbContext Scoped(TenantContext context) => new(Options(), new TenantActual(context));
    private TenantResolver Resolver() => new(Options());
    private static ClaimsPrincipal Claims(Envio envio, Usuario user) => new(new ClaimsIdentity([
        new(ClaimTypes.NameIdentifier, user.Id.ToString()), new("operador_id", envio.OperadorId.ToString()),
        new(ClaimTypes.Role, "Comercio"), new("cuenta_comercial_id", envio.CuentaComercialId.ToString())], "test"));

    private async Task<Guid> ContextSnapshotId(Guid envioId)
    {
        await using var db = Context();
        return await db.Envios.IgnoreQueryFilters().Where(e => e.Id == envioId).Select(e => EF.Property<Guid>(e, "ProgramacionKey")).SingleAsync();
    }

    private async Task<(EscenarioPersistencia A, Envio B, Usuario Ua, Usuario Ub)> DosCuentas()
    {
        var a = new EscenarioPersistencia();
        await using (var db = Context(a.Operador.Id)) { db.Add(a.Envio); await db.SaveChangesAsync(); }
        await using var writer = Context(a.Operador.Id);
        var op = await writer.Set<Operador>().SingleAsync();
        var admin = await writer.Set<Usuario>().SingleAsync(u => u.Id == a.Admin.Id);
        var zona = await writer.Set<ZonaCobertura>().Include(z => z.Franjas).SingleAsync();
        var rules = await writer.Set<ReglasOperativas>().Include(r => r.PlazosServicio).Include(r => r.PoliticaDevolucion).Include(r => r.MotivosNoEntrega).SingleAsync();
        var cuadro = await writer.Set<CuadroTarifario>().Include(c => c.Lineas).SingleAsync();
        var account = new CuentaComercial(op, new Comercio("Comercio A2", "099000003", "a2@example.com"));
        var user = new Usuario(op, "usuario-a2@example.com", RolUsuario.Comercio);
        user.AsignarCuentaComercial(account);
        var b = new Envio(account, new Destinatario("Otra persona", "099000004", "otro@example.com", new Direccion("Calle", "2", "Montevideo", "11000")),
            [new Bulto("OTRO", 3, 10, 10, 10)], ModalidadServicio.Estandar, zona, rules, cuadro, 200,
            EscenarioPersistencia.Fecha, EscenarioPersistencia.Fecha.AddHours(1),
            ProgramacionEntrega.Crear(zona.Franjas.Single(), new(2026, 10, 14), TimeSpan.FromHours(-3)), admin);
        writer.Add(b); await writer.SaveChangesAsync();
        return (a, b, a.Envio.CuentaComercial.Usuarios.Single(), user);
    }
}
