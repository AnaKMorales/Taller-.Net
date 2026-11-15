using Microsoft.Extensions.Logging.Abstractions;
using UltimaMilla.Application.Tablero;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Backoffice;
using UltimaMilla.Infrastructure.Persistence.Tablero;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task CadaCambioDeEstadoGuardadoSePublicaAlTableroDeSuOperador()
    {
        var escenario = Escenario();
        var notificador = new NotificadorEspia();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_testConnection)
            .AddInterceptors(new PublicarCambiosInterceptor(notificador, NullLogger<PublicarCambiosInterceptor>.Instance))
            .Options;

        await using (var db = new AppDbContext(options, TenantActual.ParaOperador(escenario.Operador.Id)))
        {
            db.Add(escenario.Envio);
            await db.SaveChangesAsync();
            escenario.Recibir();
            await db.SaveChangesAsync();
            // Guardar sin cambios de estado no debe avisar nada.
            await db.SaveChangesAsync();
        }

        Assert.Equal(2, notificador.Publicaciones.Count);
        var alta = Assert.Single(notificador.Publicaciones[0]);
        Assert.Equal((TipoEvento.Admitir, (EstadoEnvio?)null, EstadoEnvio.Admitido), (alta.Tipo, alta.EstadoAnterior, alta.EstadoNuevo));
        var recepcion = Assert.Single(notificador.Publicaciones[1]);
        Assert.Equal((TipoEvento.Validar, EstadoEnvio.Admitido, EstadoEnvio.EnDeposito),
            (recepcion.Tipo, recepcion.EstadoAnterior!.Value, recepcion.EstadoNuevo));
        Assert.All(notificador.Publicaciones.SelectMany(p => p), c =>
        {
            Assert.Equal(escenario.Operador.Id, c.OperadorId);
            Assert.Equal(escenario.Envio.Id, c.EnvioId);
        });
    }

    [PostgresFact]
    public async Task EnviosEnRiesgoSegunElFinDeSuVentanaYSoloDelOperador()
    {
        // La programación del escenario es el 14/10 de 08:00 a 18:00 en UTC-3: la ventana cierra a las 21:00 UTC.
        var e = Escenario();
        await using (var db = Context())
        {
            db.Add(e.Envio);
            e.Recibir();
            await db.SaveChangesAsync();
        }
        var margen = TimeSpan.FromHours(2);

        await using (var db = Context())
        {
            // 12:00 UTC: faltan 9 horas, fuera del margen.
            Assert.Empty(await new ConsultasBackoffice(db, Reloj(12)).EnviosEnRiesgoAsync(margen));

            // 20:00 UTC: cierra en una hora, por vencer.
            var porVencer = Assert.Single(await new ConsultasBackoffice(db, Reloj(20)).EnviosEnRiesgoAsync(margen));
            Assert.Equal((e.Envio.Id, EstadoEnvio.EnDeposito, false, (string?)null),
                (porVencer.EnvioId, porVencer.Estado, porVencer.Vencido, porVencer.Repartidor));
            Assert.Equal(new DateTimeOffset(2026, 10, 14, 18, 0, 0, TimeSpan.FromHours(-3)), porVencer.VentanaHasta);

            // 22:00 UTC: la ventana ya cerró.
            Assert.True(Assert.Single(await new ConsultasBackoffice(db, Reloj(22)).EnviosEnRiesgoAsync(margen)).Vencido);
        }

        await using (var otro = Context(Guid.NewGuid()))
            Assert.Empty(await new ConsultasBackoffice(otro, Reloj(22)).EnviosEnRiesgoAsync(margen));

        static TimeProvider Reloj(int horaUtc) => new RelojFijo(new DateTimeOffset(2026, 10, 14, horaUtc, 0, 0, TimeSpan.Zero));
    }

    [PostgresFact]
    public async Task LaFlotaMuestraLaUltimaPosicionInformadaPorCadaRepartidor()
    {
        var e = Escenario(reintentable: true);
        await using (var db = Context())
        {
            db.Add(e.Envio);
            await db.SaveChangesAsync();
            e.Recibir();
            var ruta = e.Asignar();
            db.Add(ruta);
            ruta.Despachar(e.Admin, EscenarioPersistencia.Fecha);
            e.Envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reglas.MotivosNoEntrega.Single(), [e.Foto()], e.Repartidor,
                EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid(), new GeoPunto(-34.9011m, -56.1645m));
            await db.SaveChangesAsync();
        }

        var ahora = new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha.AddHours(3)));
        await using (var db = Context())
        {
            var posicion = Assert.Single(await new ConsultasBackoffice(db, ahora).UltimasPosicionesAsync(TimeSpan.FromHours(12)));
            Assert.Equal((e.Repartidor.Email, -34.9011m, -56.1645m, EscenarioPersistencia.Fecha.AddHours(1)),
                (posicion.Repartidor, posicion.Latitud, posicion.Longitud, posicion.ReportadaEn));
            // Una posición más vieja que la antigüedad máxima ya no se muestra.
            Assert.Empty(await new ConsultasBackoffice(db, ahora).UltimasPosicionesAsync(TimeSpan.FromHours(1)));
        }

        await using (var otro = Context(Guid.NewGuid()))
            Assert.Empty(await new ConsultasBackoffice(otro, ahora).UltimasPosicionesAsync(TimeSpan.FromHours(12)));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    [PostgresFact]
    public async Task BackofficeMuestraIncidenciasYAvisosFallidosSoloDelOperador()
    {
        var e = Escenario(reintentable: true);
        await using (var db = Context())
        {
            db.Add(e.Envio);
            await db.SaveChangesAsync();
            e.Recibir();
            var ruta = e.Asignar();
            db.Add(ruta);
            ruta.Despachar(e.Admin, EscenarioPersistencia.Fecha);
            e.Envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reglas.MotivosNoEntrega.Single(), [e.Foto()], e.Repartidor,
                EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid(), new GeoPunto(-34.9011m, -56.1645m));
            _ = new Discrepancia(e.Envio, e.Envio.Bultos.Single(), TipoDiscrepancia.PesoNoCoincide, "Peso", "2.12", "2.5",
                EscenarioPersistencia.Fecha.AddHours(1));
            var suscripcion = new SuscripcionAviso(e.Envio.CuentaComercial, [TipoEvento.FalloEntrega], "secreto",
                new Uri("https://comercio.example/webhook?token=privado"));
            var aviso = new EntregaAviso(suscripcion, e.Envio.Eventos.Last());
            aviso.RegistrarIntento(false, EscenarioPersistencia.Fecha.AddHours(2));
            await db.SaveChangesAsync();
        }

        await using (var db = Context())
        {
            var consultas = new ConsultasBackoffice(db);
            var incidencia = Assert.Single(await consultas.IncidenciasAsync());
            Assert.Equal(e.Envio.Id, incidencia.EnvioId);
            Assert.Equal(EstadoEnvio.NoEntregado, incidencia.Estado);
            Assert.Equal(1, incidencia.IntentosFallidos);
            Assert.Equal("Ausente o rechazo", incidencia.UltimoMotivo);
            Assert.Equal(1, incidencia.Discrepancias);

            var aviso = Assert.Single(await consultas.AvisosFallidosAsync());
            Assert.Equal(e.Envio.Id, aviso.EnvioId);
            Assert.Equal("comercio.example", aviso.Destino);
            Assert.Equal(1, aviso.Intentos);

            var tablero = await consultas.TableroAsync(3);
            Assert.Equal(1, tablero.EnviosPorEstado[EstadoEnvio.NoEntregado]);
            Assert.Equal(3, tablero.UltimosCambios.Count);
            Assert.Equal(TipoEvento.FalloEntrega, tablero.UltimosCambios[0].Tipo);
        }

        // Otro operador, en la misma base, no ve nada de lo anterior.
        await using (var otro = Context(Guid.NewGuid()))
        {
            var consultas = new ConsultasBackoffice(otro);
            Assert.Empty(await consultas.IncidenciasAsync());
            Assert.Empty(await consultas.AvisosFallidosAsync());
            var tablero = await consultas.TableroAsync(20);
            Assert.Empty(tablero.EnviosPorEstado);
            Assert.Empty(tablero.UltimosCambios);
        }
    }

    [PostgresFact]
    public async Task LoginExigeLaContrasenaCorrectaYNoGuardaLaContrasenaEnClaro()
    {
        var escenario = Escenario();
        var comercio = escenario.Envio.CuentaComercial.Usuarios.Single();
        await using (var db = Context())
        {
            db.Add(escenario.Envio);
            await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, escenario.Admin, "Secreta123!");
            await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, comercio, "Comercio123!");
            await db.SaveChangesAsync();
        }
        var login = new BuscadorUsuariosLogin(Options());

        var admin = await login.ValidarAsync("  ADMIN@Example.com ", "Secreta123!");
        Assert.NotNull(admin);
        Assert.Equal((escenario.Admin.Id, escenario.Operador.Id, RolUsuario.Administrador, (Guid?)null),
            (admin.UsuarioId, admin.OperadorId, admin.Rol, admin.CuentaComercialId));

        var deComercio = await login.ValidarAsync(comercio.Email, "Comercio123!");
        Assert.Equal(escenario.Envio.CuentaComercial.Id, deComercio!.CuentaComercialId);

        Assert.Null(await login.ValidarAsync("admin@example.com", "secreta123!"));
        Assert.Null(await login.ValidarAsync("nadie@example.com", "Secreta123!"));
        // El repartidor no tiene cuenta de acceso: no puede ingresar.
        Assert.Null(await login.ValidarAsync(escenario.Repartidor.Email, "Secreta123!"));

        await using var lectura = Context();
        var guardado = await lectura.Set<CuentaAcceso>().Where(c => c.Id == escenario.Admin.Id).Select(c => c.PasswordHash).SingleAsync();
        Assert.DoesNotContain("Secreta123", guardado);
        // La política de Identity rechaza contraseñas débiles.
        await Assert.ThrowsAsync<ArgumentException>(() => FabricaCuentasAcceso.Predeterminada.CrearAsync(lectura, escenario.Admin, "corta"));
    }

    [PostgresFact]
    public async Task PortalMuestraSoloLosEnviosDeLaCuentaDelComercio()
    {
        var escenario = Escenario();
        var comercio = escenario.Envio.CuentaComercial.Usuarios.Single();
        await using (var db = Context())
        {
            db.Add(escenario.Envio);
            await db.SaveChangesAsync();
        }

        await using (var propia = new AppDbContext(Options(), new TenantActual(TenantContext.ParaUsuario(
            escenario.Operador.Id, comercio.Id, RolUsuario.Comercio, escenario.Envio.CuentaComercial.Id))))
        {
            var envio = Assert.Single(await new ConsultasPortal(propia).EnviosAsync());
            Assert.Equal(escenario.Envio.CodigoSeguimiento, envio.CodigoSeguimiento);
        }

        await using (var otraCuenta = new AppDbContext(Options(), new TenantActual(TenantContext.ParaUsuario(
            escenario.Operador.Id, comercio.Id, RolUsuario.Comercio, Guid.NewGuid()))))
            Assert.Empty(await new ConsultasPortal(otraCuenta).EnviosAsync());

        // Un usuario interno no tiene cuenta comercial: el portal no le muestra nada.
        await using (var interno = Context())
            Assert.Empty(await new ConsultasPortal(interno).EnviosAsync());
    }

    [PostgresFact]
    public async Task DevolucionesSoloParaElComercioDelEnvio()
    {
        var e = Escenario();
        var comercio = e.Envio.CuentaComercial.Usuarios.Single();
        await using (var db = Context())
        {
            db.Add(e.Envio);
            await db.SaveChangesAsync();
            e.Recibir();
            await db.SaveChangesAsync();
            var ruta = e.Asignar();
            db.Add(ruta);
            await db.SaveChangesAsync();
            ruta.Despachar(e.Admin, EscenarioPersistencia.Fecha);
            await db.SaveChangesAsync();
            e.Envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reglas.MotivosNoEntrega.Single(),
                [e.Foto()], e.Repartidor, EscenarioPersistencia.Fecha.AddHours(1), Guid.NewGuid(),
                new GeoPunto(-34.9011m, -56.1645m));
            await db.SaveChangesAsync();
            e.Envio.IniciarDevolucion(e.Admin, EscenarioPersistencia.Fecha.AddHours(1));
            await db.SaveChangesAsync();
        }

        await using (var propia = new AppDbContext(Options(), new TenantActual(TenantContext.ParaUsuario(
            e.Operador.Id, comercio.Id, RolUsuario.Comercio, e.Envio.CuentaComercial.Id))))
        {
            var devolucion = Assert.Single(await new ConsultasPortal(propia).DevolucionesAsync());
            Assert.Equal(e.Envio.CodigoSeguimiento, devolucion.CodigoSeguimiento);
            Assert.Equal(e.Reglas.MotivosNoEntrega.Single().Nombre, devolucion.Motivo);
            Assert.Equal(e.Envio.Bultos.Select(b => b.Codigo), devolucion.Bultos);
            Assert.False(devolucion.Completada);
        }

        await using (var otraCuenta = new AppDbContext(Options(), new TenantActual(TenantContext.ParaUsuario(
            e.Operador.Id, comercio.Id, RolUsuario.Comercio, Guid.NewGuid()))))
            Assert.Empty(await new ConsultasPortal(otraCuenta).DevolucionesAsync());

        await using (var interno = Context())
            Assert.Empty(await new ConsultasPortal(interno).DevolucionesAsync());
    }

    [PostgresFact]
    public async Task PorRecibirListaLosAdmitidosDelOperador()
    {
        var escenario = Escenario();
        await using (var db = Context())
        {
            db.Add(escenario.Envio);
            await db.SaveChangesAsync();
        }

        await using var lectura = Context();
        var envio = Assert.Single(await new ConsultasBackoffice(lectura).PorRecibirAsync());
        Assert.Equal(escenario.Envio.CodigoSeguimiento, envio.CodigoSeguimiento);
        Assert.Equal(escenario.Envio.Bultos.Count, envio.Bultos);
    }

    [PostgresFact]
    public async Task EtiquetaSoloParaElComercioDelEnvio()
    {
        var escenario = Escenario();
        var comercio = escenario.Envio.CuentaComercial.Usuarios.Single();
        var codigo = escenario.Envio.CodigoSeguimiento;
        await using (var db = Context())
        {
            db.Add(escenario.Envio);
            await db.SaveChangesAsync();
        }

        await using (var propia = new AppDbContext(Options(), new TenantActual(TenantContext.ParaUsuario(
            escenario.Operador.Id, comercio.Id, RolUsuario.Comercio, escenario.Envio.CuentaComercial.Id))))
        {
            var etiqueta = await new ConsultasPortal(propia).EtiquetaAsync(codigo);
            Assert.NotNull(etiqueta);
            Assert.Equal(escenario.Envio.Destinatario.Nombre, etiqueta.Destinatario);
            Assert.Equal(escenario.Envio.Bultos.Count, etiqueta.Bultos.Count);
            Assert.Null(await new ConsultasPortal(propia).EtiquetaAsync("NO-EXISTE"));
        }

        await using (var otraCuenta = new AppDbContext(Options(), new TenantActual(TenantContext.ParaUsuario(
            escenario.Operador.Id, comercio.Id, RolUsuario.Comercio, Guid.NewGuid()))))
            Assert.Null(await new ConsultasPortal(otraCuenta).EtiquetaAsync(codigo));

        await using (var interno = Context())
            Assert.Null(await new ConsultasPortal(interno).EtiquetaAsync(codigo));
    }

    private sealed class NotificadorEspia : INotificadorTablero
    {
        public List<IReadOnlyList<CambioEnvio>> Publicaciones { get; } = [];

        public Task PublicarAsync(IReadOnlyList<CambioEnvio> cambios, CancellationToken cancellationToken = default)
        {
            Publicaciones.Add(cambios);
            return Task.CompletedTask;
        }
    }
}
