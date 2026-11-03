using UltimaMilla.Application.Tenancy;
namespace UltimaMilla.Infrastructure.IntegrationTests;

// Cada prueba crea una base local temporal, migra desde cero y elimina solo esa base al terminar.
public sealed partial class PostgresTests : IAsyncLifetime
{
    private readonly string _databaseName = "ultimamilla_test_" + Guid.NewGuid().ToString("N");
    private string _adminConnection = "";
    private string _testConnection = "";
    private bool _created;
    private Guid _operatorId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("ULTIMAMILLA_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Falta la conexión local de pruebas."));
        if (builder.Host is not ("localhost" or "127.0.0.1" or "::1"))
            throw new InvalidOperationException("Estas pruebas están reservadas para PostgreSQL local.");
        _adminConnection = builder.ConnectionString;
        builder.Database = _databaseName;
        _testConnection = builder.ConnectionString;
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        _created = true;
        try
        {
            await using var db = Context();
            await db.Database.MigrateAsync();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (!_created) return;
        using var pool = new NpgsqlConnection(_testConnection);
        NpgsqlConnection.ClearPool(pool);
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        _created = false;
    }

    private AppDbContext Context(Guid? operatorId = null) => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_testConnection).Options,
        UltimaMilla.Application.Tenancy.TenantActual.ParaOperador(operatorId ?? _operatorId));

    private EscenarioPersistencia Escenario(bool reintentable = false, bool parcial = false,
        AccionBultosPendientes accion = AccionBultosPendientes.NuevaVisitaSinRecargo, decimal? recargo = null)
    {
        var scenario = new EscenarioPersistencia(reintentable, parcial: parcial, accion: accion, recargo: recargo);
        _operatorId = scenario.Operador.Id;
        return scenario;
    }

    [PostgresFact]
    public async Task MigraDesdeCeroYReconoceElHistorial()
    {
        await using var db = Context();
        Assert.True(await db.Database.CanConnectAsync());
        var esperadas = db.Database.GetMigrations().ToArray();
        var aplicadas = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.NotEmpty(esperadas);
        Assert.Equal(esperadas, aplicadas);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await using var connection = new NpgsqlConnection(_testConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'", connection);
        Assert.Equal(39L, await command.ExecuteScalarAsync());
    }

    [PostgresFact]
    public async Task GuardaYRecuperaGrafoTiposYRelacionesReales()
    {
        var escenario = Escenario();
        await using (var writer = Context())
        {
            writer.Add(escenario.Envio);
            await writer.SaveChangesAsync();
        }
        await using var reader = Context();
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(escenario.Envio.Id, actual.Id);
        Assert.Equal(2.1234567m, actual.Bultos.Single().Peso);
        Assert.Equal(150.25m, actual.TarifaCalculada);
        Assert.Equal("0010 bis", actual.Destinatario.Direccion.Numero);
        Assert.Equal("01100", actual.Destinatario.Direccion.CodigoPostal);
        Assert.Equal(DateTimeKind.Utc, actual.FechaAdmision.Kind);
        Assert.True(escenario.Envio.Programacion.InicioLocal.EqualsExact(actual.Programacion.InicioLocal));
        Assert.Same(actual.Programacion, actual.Eventos.Single().ProgramacionNueva);
        Assert.Single(actual.CuentaComercial.Usuarios);
        Assert.True(actual.Reglas.TiposPrueba.SetEquals([TipoPrueba.Foto]));
        Assert.Single(actual.CuadroTarifario.Lineas.Single().Ajustes);
        Assert.Equal("admin@example.com", await reader.Set<Usuario>().Where(u => u.Id == escenario.Admin.Id)
            .Select(u => EF.Property<string>(u, "EmailNormalizado")).SingleAsync());
    }

    [PostgresFact]
    public async Task PersisteAsignacionIntentoDevolucionRendicionYAvisos()
    {
        var e = Escenario();
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
            var devolucion = e.Envio.IniciarDevolucion(e.Admin, EscenarioPersistencia.Fecha.AddHours(1));
            EscaneoBulto.RegistrarDevolucion(devolucion, e.Envio.Bultos.Single(), e.Admin,
                EscenarioPersistencia.Fecha.AddHours(2), ruta);
            devolucion.Completar(e.Admin, EscenarioPersistencia.Fecha.AddHours(2));
            ruta.Rendir(EscenarioPersistencia.Fecha.AddHours(2));
            _ = new Discrepancia(e.Envio, e.Envio.Bultos.Single(), TipoDiscrepancia.PesoNoCoincide,
                "Peso", "2.12", "2.2", EscenarioPersistencia.Fecha.AddHours(2));
            var suscripcion = new SuscripcionAviso(e.Envio.CuentaComercial, [TipoEvento.DevolucionExitosa],
                "secreto-de-prueba", new Uri("https://comercio.example/webhook"));
            var entrega = new EntregaAviso(suscripcion, e.Envio.Eventos.Last());
            entrega.RegistrarIntento(true, EscenarioPersistencia.Fecha.AddHours(2));
            var mensaje = new Notificacion(e.Envio.Destinatario, CanalNotificacion.Email, "Devuelto");
            mensaje.MarcarEnviada(EscenarioPersistencia.Fecha.AddHours(2));
            await db.SaveChangesAsync();
        }
        await using var reader = Context();
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        Assert.Equal(EstadoEnvio.Devuelto, actual.Estado);
        Assert.Null(actual.Parada);
        Assert.Equal(EstadoDevolucion.Completado, actual.Devolucion!.Estado);
        Assert.Equal(new GeoPunto(-34.9011m, -56.1645m), actual.Intentos.Single().Pruebas.Single().Posicion);
        Assert.Equal(new ArchivoRef("evidencias/foto.jpg"), actual.Intentos.Single().Pruebas.Single().ArchivoRef);
        Assert.Contains(actual.Bultos.Single().Escaneos, scan => scan.Devolucion == actual.Devolucion);
        Assert.Single(actual.Discrepancias);
        Assert.Single(await reader.Set<Rendicion>().ToListAsync());
        Assert.NotNull(actual.Destinatario.Notificaciones.Single().EnviadaEn);
        var aviso = await reader.Set<EntregaAviso>().Include(a => a.Suscripcion).Include(a => a.Evento).SingleAsync();
        Assert.True(aviso.Exitoso);
        Assert.Equal(new Uri("https://comercio.example/webhook"), aviso.Suscripcion.UrlDestino);
        Assert.True(aviso.Suscripcion.TiposEvento.SetEquals([TipoEvento.DevolucionExitosa]));
    }

    [PostgresFact]
    public async Task ReprogramaYReasignaConservandoLasInstantaneas()
    {
        var e = Escenario(reintentable: true);
        var fecha = EscenarioPersistencia.Fecha;
        var nueva = ProgramacionEntrega.Crear(e.Zona.Franjas.Single(), DateOnly.FromDateTime(fecha.AddDays(1)), TimeSpan.FromHours(-3));
        await using (var db = Context())
        {
            db.Add(e.Envio);
            await db.SaveChangesAsync();
            e.Recibir();
            var primera = e.Asignar();
            db.Add(primera);
            await db.SaveChangesAsync();
            primera.Despachar(e.Admin, fecha);
            e.Envio.RegistrarIntento(ResultadoIntento.Fallido, e.Reglas.MotivosNoEntrega.Single(),
                [e.Foto()], e.Repartidor, fecha.AddHours(1), Guid.NewGuid());
            await db.SaveChangesAsync();
            e.Envio.Reprogramar(nueva, e.Admin, fecha.AddHours(1));
            await db.SaveChangesAsync();
            var segunda = e.Asignar(fecha.AddDays(1));
            db.Add(segunda);
            await db.SaveChangesAsync();
            Assert.Single(await db.Set<Parada>().Where(p => p.Activa).ToListAsync());
        }
        await using var reader = Context();
        var actual = await EscenarioPersistencia.Grafo(reader).SingleAsync();
        var cambio = actual.Eventos.Single(v => v.Tipo == TipoEvento.Reintentar);
        Assert.Equal(e.Envio.FechaComprometida, actual.FechaComprometida);
        Assert.Equal(new DateOnly(2026, 10, 14), cambio.ProgramacionAnterior!.FechaEntrega);
        Assert.Equal(new DateOnly(2026, 10, 15), cambio.ProgramacionNueva!.FechaEntrega);
        Assert.Same(actual.Programacion, cambio.ProgramacionNueva);
        Assert.NotNull(actual.Parada);
        Assert.Equal(2, await reader.Set<Parada>().CountAsync());
        Assert.Single(await reader.Set<Parada>().Where(p => !p.Activa).ToListAsync());
    }

    [PostgresFact]
    public async Task BaseRechazaDuplicadosAunqueNoSeCarguenLasColeccionesDeDomain()
    {
        var e = Escenario();
        await using (var db = Context())
        {
            db.Add(e.Envio);
            await db.SaveChangesAsync();
        }
        await using (var db = Context())
        {
            db.Add(new CuentaComercial(await db.Set<Operador>().SingleAsync(), await db.Set<Comercio>().SingleAsync()));
            await FallaPostgres(db.SaveChangesAsync, PostgresErrorCodes.UniqueViolation);
        }
        await using (var db = Context())
        {
            db.Add(new Usuario(await db.Set<Operador>().SingleAsync(), "ADMIN@EXAMPLE.COM", RolUsuario.Administrador));
            await FallaPostgres(db.SaveChangesAsync, PostgresErrorCodes.UniqueViolation);
        }
        await using (var db = Context())
        {
            db.Add(new Vehiculo(await db.Set<Operador>().SingleAsync(), "abc123", 100, 10));
            await FallaPostgres(db.SaveChangesAsync, PostgresErrorCodes.UniqueViolation);
        }
        await using (var db = Context())
        {
            // La escritura de un hijo también comprueba la versión del envío observado.
            await db.Envios.SingleAsync();
            var bulto = new Bulto("b001", 1, 10, 10, 10);
            db.Add(bulto);
            db.Entry(bulto).Property("EnvioKey").CurrentValue = e.Envio.Id;
            await FallaPostgres(db.SaveChangesAsync, PostgresErrorCodes.UniqueViolation);
        }
    }

    [PostgresFact]
    public async Task BaseProtegeReferenciasYBorradoRestringido()
    {
        var e = Escenario();
        await using (var db = Context())
        {
            db.Add(e.Envio);
            await db.SaveChangesAsync();
        }
        await using (var db = Context())
        {
            db.Remove(await db.Set<Operador>().SingleAsync());
            await FallaPostgres(db.SaveChangesAsync, PostgresErrorCodes.ForeignKeyViolation);
        }
        await using (var db = Context())
        {
            var id = Guid.NewGuid();
            var absent = Guid.NewGuid();
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"Bultos\" (\"Id\", \"Codigo\", \"Peso\", \"Alto\", \"Ancho\", \"Largo\", \"EnvioId\", \"TenantId\") VALUES ({id}, 'Sin dueño real', 1, 10, 10, 10, {absent}, {_operatorId})"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        }
    }

    [PostgresFact]
    public async Task RollbackRevierteTodoElAlta()
    {
        var e = Escenario();
        await using (var db = Context())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Add(e.Envio);
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.Envios.CountAsync());
            await transaction.RollbackAsync();
        }
        await using var reader = Context();
        Assert.Empty(await reader.Envios.ToListAsync());
        Assert.Empty(await reader.Set<Operador>().ToListAsync());
        Assert.Empty(await reader.Set<EventoDeEnvio>().ToListAsync());
    }

    private static async Task FallaPostgres(Func<CancellationToken, Task<int>> save, string code)
    {
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => save(CancellationToken.None));
        Assert.Equal(code, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
}
