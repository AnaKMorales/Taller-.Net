using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Deposito;
using UltimaMilla.Domain.Comercios;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Operadores;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Tests;

public sealed class CierreDepositoTests
{
    [Fact]
    public async Task DevolucionExigeTodosSusEscaneosYReconoceReenviosMientrasEstaAbierta()
    {
        var caso = new Caso();
        var handler = new DevolucionDepositoHandler(caso, new Reloj());
        Assert.Null((await handler.ConsultarAsync(caso.Envio.Id)).Devolucion);
        var inicio = await handler.IniciarAsync(caso.Envio.Id, new(caso.VersionEnvio));
        Assert.Equal(EstadoEnvio.EnDevolucion, inicio.Estado);
        Assert.Equal((2, 0, 2), (inicio.Devolucion!.Esperados, inicio.Devolucion.Recibidos, inicio.Devolucion.Faltantes));
        var solicitud = new EscanearDevolucionCommand(" b-1 ", inicio.Version);
        var escaneo = await handler.EscanearAsync(caso.Envio.Id, solicitud);
        var repetido = await handler.EscanearAsync(caso.Envio.Id, solicitud);
        Assert.False(escaneo.Repetido);
        Assert.True(repetido.Repetido);
        Assert.Equal(escaneo.Envio.Version, repetido.Envio.Version);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.CompletarAsync(caso.Envio.Id, new(caso.VersionEnvio)));
        Assert.Equal(EstadoDevolucion.EnProceso, caso.Envio.Devolucion!.Estado);
        await handler.EscanearAsync(caso.Envio.Id, new("B-2", caso.VersionEnvio));
        var final = await handler.CompletarAsync(caso.Envio.Id, new(caso.VersionEnvio));
        Assert.Equal(EstadoEnvio.Devuelto, final.Estado);
        Assert.Equal(EstadoDevolucion.Completado, final.Devolucion!.Estado);
        Assert.Equal(0, final.Devolucion.Faltantes);
        Assert.Equal(Caso.Ahora, final.Devolucion.FechaCierre);
        Assert.All(caso.Envio.Bultos, b => Assert.Equal(EstadoBulto.Devuelto, b.Estado));
        Assert.All(caso.Envio.Bultos, b => Assert.Single(b.Escaneos, e => e.Tipo == TipoEscaneo.Devolucion));
        Assert.Single(caso.Envio.Eventos, e => e.Tipo == TipoEvento.DevolucionExitosa && e.ResponsableId == caso.Responsable.Id);
        Assert.Equal(4, caso.GuardadosEnvio);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.EscanearAsync(caso.Envio.Id, solicitud));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.CompletarAsync(caso.Envio.Id, new(caso.VersionEnvio)));
    }

    [Fact]
    public async Task DevolucionParcialSoloRecibePendientesYConservaElBultoEntregado()
    {
        var caso = new Caso(parcial: true);
        var handler = new DevolucionDepositoHandler(caso, new Reloj());
        var inicio = await handler.IniciarAsync(caso.Envio.Id, new(caso.VersionEnvio));
        Assert.Equal("B-2", Assert.Single(inicio.Devolucion!.Bultos).Codigo);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.EscanearAsync(caso.Envio.Id, new("B-1", caso.VersionEnvio)));
        await handler.EscanearAsync(caso.Envio.Id, new("B-2", caso.VersionEnvio));
        var final = await handler.CompletarAsync(caso.Envio.Id, new(caso.VersionEnvio));
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, final.Estado);
        Assert.Equal(EstadoBulto.Entregado, caso.Envio.Bultos.First().Estado);
        Assert.Equal(EstadoBulto.Devuelto, caso.Envio.Bultos.Last().Estado);
        Assert.DoesNotContain(caso.Envio.Bultos.First().Escaneos, e => e.Tipo == TipoEscaneo.Devolucion);
    }

    [Fact]
    public async Task NoIniciaDevolucionSiLaPoliticaTodaviaExigeReintentar()
    {
        var caso = new Caso(reintentable: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DevolucionDepositoHandler(caso, new Reloj())
            .IniciarAsync(caso.Envio.Id, new(caso.VersionEnvio)));
        Assert.Null(caso.Envio.Devolucion);
        Assert.Equal(EstadoEnvio.NoEntregado, caso.Envio.Estado);
        Assert.Equal(0, caso.GuardadosEnvio);
    }

    [Fact]
    public async Task NoEscaneaNiCierraSiNoHayDevolucionAbierta()
    {
        var caso = new Caso();
        var handler = new DevolucionDepositoHandler(caso, new Reloj());
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.EscanearAsync(caso.Envio.Id, new("B-1", caso.VersionEnvio)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.CompletarAsync(caso.Envio.Id, new(caso.VersionEnvio)));
        Assert.Equal(0, caso.GuardadosEnvio);
    }

    [Fact]
    public async Task VersionDeDevolucionViejaNoIniciaEscaneaNiCierra()
    {
        var caso = new Caso();
        var handler = new DevolucionDepositoHandler(caso, new Reloj());
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => handler.IniciarAsync(caso.Envio.Id, new(Guid.NewGuid())));
        await handler.IniciarAsync(caso.Envio.Id, new(caso.VersionEnvio));
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => handler.EscanearAsync(caso.Envio.Id, new("B-1", Guid.NewGuid())));
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => handler.CompletarAsync(caso.Envio.Id, new(Guid.NewGuid())));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.EscanearAsync(caso.Envio.Id, new("AJENO", caso.VersionEnvio)));
        Assert.Equal(1, caso.GuardadosEnvio);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RendicionExigeRutaDespachadaSinParadasActivas(bool despachada)
    {
        var caso = new Caso(despachada: despachada, registrarIntento: false);
        var handler = new RendirRutaHandler(caso, new Reloj());
        var estado = await handler.ConsultarAsync(caso.Ruta.Id);
        Assert.Equal(1, estado.ParadasPendientes);
        Assert.Equal(7, Assert.Single(estado.Paradas).Orden);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(caso.Ruta.Id, new(caso.VersionRuta)));
        Assert.Null(caso.Ruta.Rendicion);
        Assert.Equal(0, caso.GuardadosRuta);
    }

    [Fact]
    public async Task RendicionCierraSoloLaRutaYSinCompletarUnaDevolucionPendiente()
    {
        var caso = new Caso();
        await new DevolucionDepositoHandler(caso, new Reloj()).IniciarAsync(caso.Envio.Id, new(caso.VersionEnvio));
        var handler = new RendirRutaHandler(caso, new Reloj());
        var version = caso.VersionRuta;
        using var cancelacion = new CancellationTokenSource();
        var final = await handler.Handle(caso.Ruta.Id, new(version), cancelacion.Token);
        Assert.Equal(Caso.Ahora, final.RendidaEn);
        Assert.NotNull(final.RendicionId);
        Assert.Equal(0, final.ParadasPendientes);
        Assert.Equal(EstadoDevolucion.EnProceso, Assert.Single(final.Paradas).EstadoDevolucion);
        Assert.NotEqual(version, final.Version);
        Assert.Equal(1, caso.GuardadosRuta);
        Assert.Equal(cancelacion.Token, caso.Token);
        Assert.Equal(EstadoEnvio.EnDevolucion, caso.Envio.Estado);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(caso.Ruta.Id, new(caso.VersionRuta)));
    }

    [Fact]
    public async Task RendicionRechazaVersionObsoletaYFechaAnteriorAlDespacho()
    {
        var caso = new Caso();
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => new RendirRutaHandler(caso, new Reloj())
            .Handle(caso.Ruta.Id, new(Guid.NewGuid())));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RendirRutaHandler(caso,
            new Reloj(Caso.Inicio.AddSeconds(-1))).Handle(caso.Ruta.Id, new(caso.VersionRuta)));
        Assert.Equal(0, caso.GuardadosRuta);
    }

    [Fact]
    public async Task SolicitudesIncompletasNoAbrenUnaUnidadDeEscritura()
    {
        var caso = new Caso();
        var dev = new DevolucionDepositoHandler(caso, new Reloj());
        var rend = new RendirRutaHandler(caso, new Reloj());
        await Assert.ThrowsAsync<ArgumentException>(() => dev.ConsultarAsync(Guid.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => rend.ConsultarAsync(Guid.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => dev.IniciarAsync(caso.Envio.Id, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => dev.EscanearAsync(caso.Envio.Id, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => dev.CompletarAsync(caso.Envio.Id, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => rend.Handle(caso.Ruta.Id, null!));
        await Assert.ThrowsAsync<ArgumentException>(() => dev.IniciarAsync(caso.Envio.Id, new(Guid.Empty)));
        await Assert.ThrowsAsync<ArgumentException>(() => dev.CompletarAsync(caso.Envio.Id, new(Guid.Empty)));
        await Assert.ThrowsAsync<ArgumentException>(() => dev.EscanearAsync(caso.Envio.Id, new("B-1", Guid.Empty)));
        await Assert.ThrowsAsync<ArgumentException>(() => dev.EscanearAsync(caso.Envio.Id, new(" ", caso.VersionEnvio)));
        await Assert.ThrowsAsync<ArgumentException>(() => rend.Handle(caso.Ruta.Id, new(Guid.Empty)));
        Assert.Equal(0, caso.Dispuestos);
    }

    private sealed class Caso : IDevolucionDepositoRepository, IRendicionRutaRepository
    {
        internal static readonly DateTime Inicio = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        internal static readonly DateTime Ahora = Inicio.AddHours(2);
        public Envio Envio { get; }
        public Ruta Ruta { get; }
        public Usuario Responsable { get; }
        public Guid VersionEnvio { get; set; } = Guid.NewGuid();
        public Guid VersionRuta { get; set; } = Guid.NewGuid();
        public int GuardadosEnvio { get; set; }
        public int GuardadosRuta { get; set; }
        public int Dispuestos { get; set; }
        public CancellationToken Token { get; set; }
        public Caso(bool parcial = false, bool reintentable = false, bool despachada = true, bool registrarIntento = true)
        {
            var operador = new Operador("Operador");
            Responsable = new Usuario(operador, "deposito@example.com", RolUsuario.OperarioDeposito);
            var repartidor = new Repartidor(operador, "repartidor@example.com");
            var zona = new ZonaCobertura(operador, "Centro", "11000", [new("Día", new(8, 0), new(18, 0))]);
            var reglas = new ReglasOperativas(operador, 1, Inicio.AddDays(-1), 2, 0, [TipoPrueba.Foto], new(1, 5, true),
                parcial, parcial ? AccionBultosPendientes.DevolverAlComercio : AccionBultosPendientes.NuevaVisitaSinRecargo);
            _ = new PlazoServicio(reglas, ModalidadServicio.Estandar, 24, new(16, 0));
            var motivo = new MotivoNoEntrega(reglas, "Incidencia", "Motivo de prueba", UltimaMilla.Domain.Configuracion.Responsable.Destinatario, false, reintentable);
            var cuadro = new CuadroTarifario(operador, 1, Inicio.AddDays(-1));
            _ = new LineaTarifa(cuadro, zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
            Envio = new(new CuentaComercial(operador, new Comercio("Comercio", "099123456", "c@example.com")),
                new("Persona", "099123456", "p@example.com", new("Calle", "1", "Montevideo", "11000")),
                [new("B-1", 1, 10, 10, 10), new("B-2", 1, 10, 10, 10)], ModalidadServicio.Estandar, zona, reglas, cuadro, 150,
                Inicio, Inicio.AddHours(1), ProgramacionEntrega.Crear(zona.Franjas.Single(), new(2026, 11, 14), TimeSpan.Zero), Responsable);
            foreach (var b in Envio.Bultos) EscaneoBulto.RegistrarRecepcion(b, Responsable, Inicio);
            Envio.RecibirEnDeposito(Responsable, Inicio);
            Ruta = new(repartidor, new Vehiculo(operador, "ABC123", 100, 10), 10);
            var parada = Ruta.AgregarParada(Envio, 7, Inicio.AddHours(1), Responsable, Inicio);
            if (!despachada) return;
            Ruta.Despachar(Responsable, Inicio);
            if (!registrarIntento) return;
            if (parcial)
            {
                var foto = new PruebaEntrega(TipoPrueba.Foto, Inicio.AddHours(1), new GeoPunto(-34, -56), new ArchivoRef("foto.jpg"));
                Envio.RegistrarIntentoPorBulto(parada.Id, [
                    new(Envio.Bultos.First().Id, ResultadoIntento.Entregado, null, [foto]),
                    new(Envio.Bultos.Last().Id, ResultadoIntento.Fallido, motivo, [])], repartidor, Inicio.AddHours(1), Guid.NewGuid());
            }
            else Envio.RegistrarIntento(ResultadoIntento.Fallido, motivo, [], repartidor, Inicio.AddHours(1), Guid.NewGuid());
        }
        public Task<IOperacionDevolucionDeposito> AbrirAsync(Guid id, CancellationToken ct) => Task.FromResult<IOperacionDevolucionDeposito>(new OpDevolucion(this));
        public Task<IOperacionRendicionRuta> AbrirRutaAsync(Guid id, CancellationToken ct) => Task.FromResult<IOperacionRendicionRuta>(new OpRendicion(this));
        public Task<IReadOnlyList<DevolucionAbiertaDto>> ListarAbiertasAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RutaPorRendirDto>> ListarAsync(CancellationToken ct) => throw new NotSupportedException();
        private sealed class OpDevolucion(Caso caso) : IOperacionDevolucionDeposito
        {
            public Envio Envio => caso.Envio;
            public Usuario Responsable => caso.Responsable;
            public Guid Version => caso.VersionEnvio;
            public Task GuardarAsync(CancellationToken ct)
            { caso.Token = ct; caso.GuardadosEnvio++; caso.VersionEnvio = Guid.NewGuid(); return Task.CompletedTask; }
            public ValueTask DisposeAsync() { caso.Dispuestos++; return ValueTask.CompletedTask; }
        }
        private sealed class OpRendicion(Caso caso) : IOperacionRendicionRuta
        {
            public Ruta Ruta => caso.Ruta;
            public Guid Version => caso.VersionRuta;
            public Task GuardarAsync(CancellationToken ct)
            { caso.Token = ct; caso.GuardadosRuta++; caso.VersionRuta = Guid.NewGuid(); return Task.CompletedTask; }
            public ValueTask DisposeAsync() { caso.Dispuestos++; return ValueTask.CompletedTask; }
        }
    }
    private sealed class Reloj(DateTime? ahora = null) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(ahora ?? Caso.Ahora); }
}
