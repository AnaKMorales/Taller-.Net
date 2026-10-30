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
            ContrasenasUsuario.Asignar(db, escenario.Admin, "Secreta123");
            ContrasenasUsuario.Asignar(db, comercio, "Comercio123");
            await db.SaveChangesAsync();
        }
        var login = new BuscadorUsuariosLogin(Options());

        var admin = await login.ValidarAsync("  ADMIN@Example.com ", "Secreta123");
        Assert.NotNull(admin);
        Assert.Equal((escenario.Admin.Id, escenario.Operador.Id, RolUsuario.Administrador, (Guid?)null),
            (admin.UsuarioId, admin.OperadorId, admin.Rol, admin.CuentaComercialId));

        var deComercio = await login.ValidarAsync(comercio.Email, "Comercio123");
        Assert.Equal(escenario.Envio.CuentaComercial.Id, deComercio!.CuentaComercialId);

        Assert.Null(await login.ValidarAsync("admin@example.com", "secreta123"));
        Assert.Null(await login.ValidarAsync("nadie@example.com", "Secreta123"));
        // El repartidor no tiene contraseña asignada: no puede ingresar.
        Assert.Null(await login.ValidarAsync(escenario.Repartidor.Email, ""));

        await using var lectura = Context();
        var guardado = await lectura.Set<Usuario>().Where(u => u.Id == escenario.Admin.Id)
            .Select(u => EF.Property<string>(u, ContrasenasUsuario.Columna)).SingleAsync();
        Assert.DoesNotContain("Secreta123", guardado);
        Assert.Throws<ArgumentException>(() => ContrasenasUsuario.Asignar(lectura, escenario.Admin, "corta"));
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
