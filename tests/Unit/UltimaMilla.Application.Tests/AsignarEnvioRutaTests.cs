using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Domain.Comercios;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Operadores;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Tests;

public sealed class AsignarEnvioRutaTests
{
    [Fact]
    public async Task AsignaTodosLosBultosConOrdenManualYConservaProgramacionYTarifa()
    {
        var caso = new Caso();
        var programacion = caso.Envio.Programacion;
        using var cancelacion = new CancellationTokenSource();
        var resultado = await new AsignarEnvioRutaHandler(caso, new Reloj()).Handle(caso.Ruta.Id,
            caso.Solicitud with { Orden = 7 }, cancelacion.Token);
        Assert.Equal(7, resultado.Orden);
        Assert.Equal(caso.Envio.Bultos.Select(b => b.Id), resultado.Bultos);
        Assert.Equal((2m, 0.002m), (resultado.PesoKg, resultado.VolumenM3));
        Assert.Same(programacion, caso.Envio.Programacion);
        Assert.Equal(150, caso.Envio.TarifaCalculada);
        Assert.Equal(EstadoEnvio.AsignadoARuta, caso.Envio.Estado);
        Assert.Equal(resultado.ParadaId, caso.Envio.Parada!.Id);
        Assert.Equal(caso.VersionEnvio, resultado.VersionEnvio);
        Assert.Equal(caso.VersionRuta, resultado.VersionRuta);
        Assert.Equal(1, caso.Guardados);
        Assert.Equal(1, caso.Dispuestos);
        Assert.Equal(cancelacion.Token, caso.Token);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VerificaVersionDeAmbosAgregadosAntesDeAsignar(bool envio)
    {
        var caso = new Caso();
        var solicitud = envio ? caso.Solicitud with { VersionEsperadaEnvio = Guid.NewGuid() } :
            caso.Solicitud with { VersionEsperadaRuta = Guid.NewGuid() };
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() =>
            new AsignarEnvioRutaHandler(caso, new Reloj()).Handle(caso.Ruta.Id, solicitud));
        Assert.Empty(caso.Ruta.Paradas);
        Assert.Null(caso.Envio.Parada);
        Assert.Equal(0, caso.Guardados);
    }

    [Theory]
    [InlineData("peso")]
    [InlineData("volumen")]
    [InlineData("paradas")]
    [InlineData("orden")]
    [InlineData("ventana")]
    [InlineData("pasado")]
    [InlineData("inactivo")]
    [InlineData("asignado")]
    [InlineData("admitido")]
    [InlineData("despachada")]
    public async Task RestriccionesNoDejanUnaAsignacionParcial(string motivo)
    {
        var caso = new Caso(motivo == "peso" ? 1 : 10, motivo == "volumen" ? 0.001m : 1,
            maxParadas: motivo == "paradas" ? 1 : 10, recibir: motivo != "admitido");
        if (motivo is "paradas" or "orden" or "despachada")
        {
            caso.Ruta.AgregarParada(caso.OtroEnvio(), 1, Caso.Ahora.AddHours(1), caso.Responsable, Caso.Ahora);
            if (motivo == "despachada") caso.Ruta.Despachar(caso.Responsable, Caso.Ahora);
        }
        if (motivo == "inactivo") caso.Ruta.Repartidor.Desactivar();
        if (motivo == "asignado") caso.Ruta.AgregarParada(caso.Envio, 1, Caso.Ahora.AddHours(1), caso.Responsable, Caso.Ahora);
        var solicitud = caso.Solicitud with { Orden = motivo == "paradas" ? 2 : 1,
            LlegadaEstimada = motivo == "ventana" ? new(caso.Envio.VentanaHasta) :
                motivo == "pasado" ? new(Caso.Ahora.AddSeconds(-1)) : caso.Solicitud.LlegadaEstimada };
        var anteriores = caso.Ruta.Paradas.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AsignarEnvioRutaHandler(caso, new Reloj())
            .Handle(caso.Ruta.Id, solicitud));
        Assert.Equal(anteriores, caso.Ruta.Paradas.Count);
        Assert.Equal(0, caso.Guardados);
        Assert.Equal(1, caso.Dispuestos);
    }

    [Fact]
    public async Task ReasignaEnvioAplazadoConLaProgramacionQueYaTenia()
    {
        var caso = new Caso();
        var anterior = new Ruta(caso.Ruta.Repartidor, caso.Ruta.Vehiculo, 5);
        anterior.AgregarParada(caso.Envio, 1, Caso.Ahora.AddHours(1), caso.Responsable, Caso.Ahora);
        var programacion = ProgramacionEntrega.Crear(caso.Envio.Zona.Franjas.Single(), new(2026, 11, 15), TimeSpan.Zero);
        caso.Envio.Aplazar(programacion, caso.Responsable, Caso.Ahora);
        var resultado = await new AsignarEnvioRutaHandler(caso, new Reloj()).Handle(caso.Ruta.Id,
            caso.Solicitud with { LlegadaEstimada = new(programacion.VentanaDesde) });
        Assert.Equal(resultado.ParadaId, caso.Envio.Parada!.Id);
        Assert.Same(programacion, caso.Envio.Programacion);
        Assert.Contains(caso.Envio.Eventos, e => e.Tipo == TipoEvento.Reasignar);
        Assert.False(Assert.Single(anterior.Paradas).Activa);
    }

    [Fact]
    public async Task SolicitudIncompletaNoAbrePersistencia()
    {
        var caso = new Caso();
        var handler = new AsignarEnvioRutaHandler(caso, new Reloj());
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(caso.Ruta.Id, null!));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(Guid.Empty, caso.Solicitud));
        foreach (var solicitud in new[] { caso.Solicitud with { EnvioId = Guid.Empty },
            caso.Solicitud with { VersionEsperadaEnvio = Guid.Empty }, caso.Solicitud with { VersionEsperadaRuta = Guid.Empty },
            caso.Solicitud with { Orden = 0 }, caso.Solicitud with { LlegadaEstimada = default } })
            await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(caso.Ruta.Id, solicitud));
        Assert.Equal(0, caso.Dispuestos);
    }

    private sealed class Caso : IAsignacionEnvioRutaRepository, IOperacionAsignacionEnvioRuta
    {
        internal static readonly DateTime Ahora = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        public Envio Envio { get; }
        public Ruta Ruta { get; }
        public Usuario Responsable { get; }
        public Guid VersionEnvio { get; private set; } = Guid.NewGuid();
        public Guid VersionRuta { get; private set; } = Guid.NewGuid();
        public int Guardados { get; private set; }
        public int Dispuestos { get; private set; }
        public CancellationToken Token { get; private set; }
        public AsignarEnvioRutaCommand Solicitud => new(Envio.Id, VersionEnvio, VersionRuta, 1, new(Ahora.AddHours(1)));
        public Caso(decimal peso = 10, decimal volumen = 1, int maxParadas = 10, bool recibir = true)
        {
            var operador = new Operador("Operador");
            Responsable = new Usuario(operador, "admin@example.com", RolUsuario.Administrador);
            var zona = new ZonaCobertura(operador, "Centro", "11000", [new("Día", new(8, 0), new(18, 0))]);
            var reglas = new ReglasOperativas(operador, 1, Ahora.AddDays(-1), 2, 0, [TipoPrueba.Foto], new(1, 5, true));
            _ = new PlazoServicio(reglas, ModalidadServicio.Estandar, 24, new(16, 0));
            var cuadro = new CuadroTarifario(operador, 1, Ahora.AddDays(-1));
            _ = new LineaTarifa(cuadro, zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
            Envio = new(new CuentaComercial(operador, new Comercio("Comercio", "099123456", "c@example.com")),
                new("Persona", "099123456", "p@example.com", new("Calle", "1", "Montevideo", "11000")),
                [new("B-1", 1, 10, 10, 10), new("B-2", 1, 10, 10, 10)], ModalidadServicio.Estandar, zona, reglas, cuadro,
                150, Ahora, Ahora.AddHours(1), ProgramacionEntrega.Crear(zona.Franjas.Single(), new(2026, 11, 14), TimeSpan.Zero), Responsable);
            if (recibir) Recibir(Envio);
            Ruta = new(new Repartidor(operador, "repartidor@example.com"), new Vehiculo(operador, "ABC123", peso, volumen), maxParadas);
        }
        public Envio OtroEnvio()
        {
            var envio = new Envio(Envio.CuentaComercial,
                new("Otra", "099123456", "otra@example.com", new("Calle", "2", "Montevideo", "11000")),
                [new("OTRO", 1, 10, 10, 10)], Envio.ModalidadServicio, Envio.Zona, Envio.Reglas, Envio.CuadroTarifario,
                150, Ahora, Ahora.AddHours(1), Envio.Programacion, Responsable);
            Recibir(envio);
            return envio;
        }
        private void Recibir(Envio envio)
        {
            foreach (var bulto in envio.Bultos) EscaneoBulto.RegistrarRecepcion(bulto, Responsable, Ahora);
            envio.RecibirEnDeposito(Responsable, Ahora);
        }
        public Task<IOperacionAsignacionEnvioRuta> AbrirAsync(Guid envioId, Guid rutaId, CancellationToken ct)
        { Token = ct; return Task.FromResult<IOperacionAsignacionEnvioRuta>(this); }
        public Task<IReadOnlyList<EnvioAsignableDto>> ListarEnviosAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RutaAsignacionDto>> ListarRutasAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<RecursosAsignacionDto> RecursosAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task GuardarAsync(CancellationToken ct)
        { Token = ct; Guardados++; VersionEnvio = Guid.NewGuid(); VersionRuta = Guid.NewGuid(); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Dispuestos++; return ValueTask.CompletedTask; }
    }
    private sealed class Reloj : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Caso.Ahora); }
}
