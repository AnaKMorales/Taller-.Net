using UltimaMilla.Application.Tenancy;
namespace UltimaMilla.Infrastructure.Tests;

// InMemory permite comprobar constructores, fixup y colecciones. No valida restricciones SQL.
public sealed class MaterializacionTests
{
    private static DbContextOptions<AppDbContext> Options()
    {
        using var schema = new AppDbContextFactory().CreateDbContext([]);
        // Usar el modelo real evita que InMemory intente remapear integer[] como una colección JSON.
        return new DbContextOptionsBuilder<AppDbContext>().UseModel(schema.Model)
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    }

    [Fact]
    public void RehidrataGrafoSinDuplicarAltasYPermiteContinuarElFlujo()
    {
        var fixture = new Datos();
        var envio = fixture.Envio;
        var options = Options();
        using (var writer = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id)))
        {
            writer.Add(envio);
            writer.SaveChanges();
        }
        using var reader = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id));
        var actual = Cargar(reader);
        Assert.NotSame(envio, actual);
        Assert.Equal(envio.Id, actual.Id);
        Assert.Single(actual.Eventos);
        Assert.Single(actual.CuentaComercial.Envios);
        Assert.Same(actual, actual.Destinatario.Envio);
        Assert.Same(actual.Destinatario, actual.Destinatario.Direccion.Destinatario);
        Assert.Same(actual, actual.Bultos.Single().Envio);
        Assert.Same(actual.Programacion, actual.Eventos.Single().ProgramacionNueva);
        Assert.Equal(TimeSpan.FromHours(-3), actual.Programacion.InicioLocal.Offset);
        Assert.True(actual.Reglas.TiposPrueba.SetEquals([TipoPrueba.Foto]));
        var responsable = actual.Eventos.Single().Responsable;
        EscaneoBulto.RegistrarRecepcion(actual.Bultos.Single(), responsable, Datos.Fecha);
        actual.RecibirEnDeposito(responsable, Datos.Fecha);
        reader.SaveChanges();
        Assert.Equal(EstadoEnvio.EnDeposito, actual.Estado);
        Assert.Equal(2, actual.Eventos.Count);
    }

    [Fact]
    public void ConservaProgramacionesAnteriorYNuevaEntreContextos()
    {
        var fixture = new Datos();
        fixture.Asignar();
        var anterior = fixture.Envio.Programacion;
        var nueva = ProgramacionEntrega.Crear(fixture.Zona.Franjas.Single(), new DateOnly(2026, 10, 15), TimeSpan.FromHours(-3));
        fixture.Envio.Aplazar(nueva, fixture.Admin, Datos.Fecha);
        var options = Options();
        using (var writer = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id)))
        {
            writer.Add(fixture.Operador);
            writer.SaveChanges();
        }
        using var reader = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id));
        var actual = Cargar(reader);
        var cambio = actual.Eventos.Single(e => e.Tipo == TipoEvento.Aplazar);
        Assert.Equal(anterior.InicioLocal, cambio.ProgramacionAnterior!.InicioLocal);
        Assert.Equal(nueva.InicioLocal, cambio.ProgramacionNueva!.InicioLocal);
        Assert.Same(actual.Programacion, cambio.ProgramacionNueva);
        Assert.Null(actual.Parada);
        Assert.Equal(fixture.Envio.FechaComprometida, actual.FechaComprometida);
        Assert.Single(reader.Set<Parada>().Where(p => !p.Activa));
    }

    [Fact]
    public void RehidrataRepartidorYCuentasDeUsuariosSinConvertirlosEnUsuarioBase()
    {
        var fixture = new Datos();
        fixture.Asignar();
        var usuario = new Usuario(fixture.Operador, "comercio@test.example", RolUsuario.Comercio);
        usuario.AsignarCuentaComercial(fixture.Envio.CuentaComercial);
        var options = Options();
        using (var writer = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id)))
        {
            writer.Add(fixture.Operador);
            writer.SaveChanges();
        }
        using var reader = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id));
        var ruta = reader.Set<Ruta>().Include(r => r.Repartidor).Include(r => r.Vehiculo).Single();
        Assert.IsType<Repartidor>(ruta.Repartidor);
        Assert.Equal(RolUsuario.Repartidor, ruta.Repartidor.Rol);
        var cargado = reader.Set<Usuario>().Include(u => u.CuentasComerciales)
            .Single(u => u.Id == usuario.Id);
        Assert.Single(cargado.CuentasComerciales);
        Assert.Contains(cargado, cargado.CuentasComerciales.Single().Usuarios);
    }

    [Fact]
    public void ConservaIntentoPruebaYDevolucion()
    {
        var fixture = new Datos();
        var ruta = fixture.Asignar();
        ruta.Despachar(fixture.Admin, Datos.Fecha);
        var motivo = fixture.Reglas.MotivosNoEntrega.Single();
        var prueba = new PruebaEntrega(TipoPrueba.Foto, Datos.Fecha.AddHours(1),
            new GeoPunto(-34.9m, -56.2m), new ArchivoRef("prueba/foto.jpg"));
        fixture.Envio.RegistrarIntento(ResultadoIntento.Fallido, motivo, [prueba], fixture.Repartidor,
            Datos.Fecha.AddHours(1), Guid.NewGuid(), new GeoPunto(-34.9m, -56.2m));
        fixture.Envio.IniciarDevolucion(fixture.Admin, Datos.Fecha.AddHours(1));
        var options = Options();
        using (var writer = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id)))
        {
            writer.Add(fixture.Operador);
            writer.SaveChanges();
        }
        using var reader = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id));
        var actual = Cargar(reader);
        var intento = actual.Intentos.Single();
        Assert.Equal(ResultadoIntento.Fallido, intento.Resultado);
        Assert.Equal("Rechazo", intento.MotivoNoEntrega!.Nombre);
        Assert.Same(actual, intento.Parada.Envio);
        Assert.Equal(prueba.ArchivoRef, intento.Pruebas.Single().ArchivoRef);
        Assert.Equal(prueba.Posicion, intento.Pruebas.Single().Posicion);
        Assert.NotNull(actual.Devolucion);
        Assert.Same(actual, actual.Devolucion.Envio);
        Assert.Equal(EstadoEnvio.EnDevolucion, actual.Estado);
    }

    [Fact]
    public void RehidrataEscaneosDeDevolucionRendicionDiscrepanciasYAvisos()
    {
        var fixture = new Datos();
        var ruta = fixture.Asignar();
        ruta.Despachar(fixture.Admin, Datos.Fecha);
        fixture.Envio.RegistrarIntento(ResultadoIntento.Fallido, fixture.Reglas.MotivosNoEntrega.Single(),
            [], fixture.Repartidor, Datos.Fecha.AddHours(1), Guid.NewGuid());
        var devolucion = fixture.Envio.IniciarDevolucion(fixture.Admin, Datos.Fecha.AddHours(1));
        var bulto = fixture.Envio.Bultos.Single();
        EscaneoBulto.RegistrarDevolucion(devolucion, bulto, fixture.Admin, Datos.Fecha.AddHours(2), ruta);
        devolucion.Completar(fixture.Admin, Datos.Fecha.AddHours(2));
        ruta.Rendir(Datos.Fecha.AddHours(2));
        _ = new Discrepancia(fixture.Envio, bulto, TipoDiscrepancia.PesoNoCoincide,
            "Peso diferente", "2 kg", "2.1 kg", Datos.Fecha.AddHours(2));
        var suscripcion = new SuscripcionAviso(fixture.Envio.CuentaComercial, [TipoEvento.DevolucionExitosa],
            "secreto-de-prueba", new Uri("https://comercio.example/webhook"));
        var entrega = new EntregaAviso(suscripcion, fixture.Envio.Eventos.Last());
        entrega.RegistrarIntento(true, Datos.Fecha.AddHours(2));
        var notificacion = new Notificacion(fixture.Envio.Destinatario, CanalNotificacion.Email, "Envío devuelto");
        notificacion.MarcarEnviada(Datos.Fecha.AddHours(2));
        var options = Options();
        using (var writer = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id)))
        {
            writer.Add(fixture.Operador);
            writer.SaveChanges();
        }
        using var reader = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id));
        var actual = Cargar(reader);
        var cierre = reader.Set<Rendicion>().Include(r => r.Ruta).Single();
        var discrepancia = reader.Set<Discrepancia>().Include(d => d.Bulto).Single();
        var aviso = reader.Set<EntregaAviso>().Include(a => a.Suscripcion).Include(a => a.Evento).Single();
        var mensaje = reader.Set<Notificacion>().Include(n => n.Destinatario).Single();
        Assert.Equal(EstadoEnvio.Devuelto, actual.Estado);
        Assert.Equal(EstadoDevolucion.Completado, actual.Devolucion!.Estado);
        Assert.Contains(actual.Bultos.Single().Escaneos, e => e.Devolucion == actual.Devolucion);
        Assert.Equal(ruta.Id, cierre.Ruta.Id);
        Assert.Same(actual.Bultos.Single(), discrepancia.Bulto);
        Assert.Equal(new Uri("https://comercio.example/webhook"), aviso.Suscripcion.UrlDestino);
        Assert.True(aviso.Suscripcion.TiposEvento.SetEquals([TipoEvento.DevolucionExitosa]));
        Assert.True(aviso.Exitoso);
        Assert.Same(actual, aviso.Evento.Envio);
        Assert.Same(actual.Destinatario, mensaje.Destinatario);
        Assert.NotNull(mensaje.EnviadaEn);
        Assert.Equal(TipoAjuste.Recargo, actual.CuadroTarifario.Lineas.Single().Ajustes.Single().Tipo);
    }

    [Fact]
    public void UltimoIntentoSeResuelvePorNumeroAunqueLaColeccionSeCargueAlReves()
    {
        var fixture = new Datos(permitirReintentos: true);
        fixture.Asignar().Despachar(fixture.Admin, Datos.Fecha);
        fixture.Envio.RegistrarIntento(ResultadoIntento.Fallido,
            fixture.Reglas.MotivosNoEntrega.Single(m => m.EsReintentable), [], fixture.Repartidor,
            Datos.Fecha.AddHours(1), Guid.NewGuid());
        fixture.Envio.Reprogramar(fixture.Envio.Programacion, fixture.Admin, Datos.Fecha.AddHours(1));
        var segundaRuta = new Ruta(fixture.Repartidor, new Vehiculo(fixture.Operador, "ABC124", 100, 10), 10);
        segundaRuta.AgregarParada(fixture.Envio, 1, Datos.Fecha.AddHours(2), fixture.Admin, Datos.Fecha.AddHours(1));
        segundaRuta.Despachar(fixture.Admin, Datos.Fecha.AddHours(1));
        fixture.Envio.RegistrarIntento(ResultadoIntento.Fallido,
            fixture.Reglas.MotivosNoEntrega.Single(m => !m.EsReintentable), [], fixture.Repartidor,
            Datos.Fecha.AddHours(2), Guid.NewGuid());
        var options = Options();
        using (var writer = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id)))
        {
            writer.Add(fixture.Operador);
            writer.SaveChanges();
        }
        using var reader = new AppDbContext(options, UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(fixture.Operador.Id));
        reader.Set<IntentoEntrega>().OrderByDescending(i => i.Numero).Load();
        var actual = Cargar(reader);
        Assert.Equal([2, 1], actual.Intentos.Select(i => i.Numero));
        var responsable = actual.Eventos.First(e => e.Tipo == TipoEvento.Admitir).Responsable;
        Assert.Throws<InvalidOperationException>(() => actual.Reprogramar(actual.Programacion, responsable, Datos.Fecha.AddHours(2)));
        Assert.NotNull(actual.IniciarDevolucion(responsable, Datos.Fecha.AddHours(2)));
    }

    private static Envio Cargar(AppDbContext db) => db.Envios
        .Include(e => e.CuentaComercial).ThenInclude(c => c.Operador)
        .Include(e => e.CuentaComercial).ThenInclude(c => c.Comercio)
        .Include(e => e.Destinatario).ThenInclude(d => d.Direccion)
        .Include(e => e.Zona).ThenInclude(z => z.Franjas)
        .Include(e => e.Reglas).ThenInclude(r => r.PoliticaDevolucion)
        .Include(e => e.Reglas).ThenInclude(r => r.PlazosServicio)
        .Include(e => e.Reglas).ThenInclude(r => r.MotivosNoEntrega)
        .Include(e => e.CuadroTarifario).ThenInclude(c => c.Lineas).ThenInclude(l => l.Ajustes)
        .Include(e => e.Programacion).ThenInclude(p => p.Franja)
        .Include(e => e.Bultos).ThenInclude(b => b.Escaneos)
        .Include(e => e.Eventos).ThenInclude(v => v.Responsable)
        .Include(e => e.Eventos).ThenInclude(v => v.ProgramacionAnterior).ThenInclude(p => p!.Franja)
        .Include(e => e.Eventos).ThenInclude(v => v.ProgramacionNueva).ThenInclude(p => p!.Franja)
        .Include(e => e.Intentos).ThenInclude(i => i.Pruebas)
        .Include(e => e.Intentos).ThenInclude(i => i.MotivoNoEntrega)
        .Include(e => e.Intentos).ThenInclude(i => i.Parada)
        .Include(e => e.Devolucion)
        .Single();

    private sealed class Datos
    {
        internal static readonly DateTime Fecha = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
        internal Operador Operador { get; } = new("Operador");
        internal Usuario Admin { get; }
        internal Repartidor Repartidor { get; }
        internal ZonaCobertura Zona { get; }
        internal ReglasOperativas Reglas { get; }
        internal Envio Envio { get; }

        internal Datos(bool permitirReintentos = false)
        {
            Admin = new Usuario(Operador, "admin@test.example", RolUsuario.Administrador);
            Repartidor = new Repartidor(Operador, "repartidor@test.example");
            var cuenta = new CuentaComercial(Operador, new Comercio("Comercio", "099000001", "test@test.example"));
            Zona = new ZonaCobertura(Operador, "Centro", "11000", [new FranjaHoraria("Día", new(8, 0), new(18, 0))]);
            Reglas = new ReglasOperativas(Operador, 1, Fecha.AddDays(-1), permitirReintentos ? 3 : 2, 0,
                [TipoPrueba.Foto], new PoliticaDevolucion(1, 5, true));
            _ = new PlazoServicio(Reglas, ModalidadServicio.Estandar, 24, new(16, 0));
            _ = new MotivoNoEntrega(Reglas, "Rechazo", "Rechazo", Responsable.Destinatario, false, false);
            if (permitirReintentos)
                _ = new MotivoNoEntrega(Reglas, "Ausente", "Ausente", Responsable.Destinatario, false, true);
            var cuadro = new CuadroTarifario(Operador, 1, Fecha.AddDays(-1));
            var linea = new LineaTarifa(cuadro, Zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
            _ = new AjusteTarifario(linea, TipoAjuste.Recargo, 10);
            var destinatario = new Destinatario("Persona", "099000002", "persona@test.example",
                new Direccion("Calle", "1", "Montevideo", "11000"));
            Envio = new Envio(cuenta, destinatario, [new Bulto("B001", 2, 10, 20, 30)],
                ModalidadServicio.Estandar, Zona, Reglas, cuadro, 150, Fecha, Fecha.AddHours(1),
                ProgramacionEntrega.Crear(Zona.Franjas.Single(), new(2026, 10, 14), TimeSpan.FromHours(-3)), Admin);
        }

        internal Ruta Asignar()
        {
            EscaneoBulto.RegistrarRecepcion(Envio.Bultos.Single(), Admin, Fecha);
            Envio.RecibirEnDeposito(Admin, Fecha);
            var ruta = new Ruta(Repartidor, new Vehiculo(Operador, "ABC123", 100, 10), 10);
            ruta.AgregarParada(Envio, 1, Fecha.AddHours(1), Admin, Fecha);
            return ruta;
        }
    }
}
