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

public sealed class DespacharRutaTests
{
    [Fact]
    public async Task CargaPorCodigoNormalizadoReconoceReenvioSinGuardarNiCambiarVersion()
    {
        var caso = new Caso(cargar: false);
        var handler = new RegistrarCargaRutaHandler(caso, new Reloj());
        var antes = await handler.ConsultarAsync(caso.Ruta.Id);
        Assert.Equal(1, antes.Faltantes);
        var primero = await handler.Handle(caso.Ruta.Id, new(" b-1 ", antes.Version));
        Assert.False(primero.Repetido);
        Assert.Equal(0, primero.Faltantes);
        var repetido = await handler.Handle(caso.Ruta.Id, new("B-1", antes.Version));
        Assert.True(repetido.Repetido);
        Assert.Equal(primero.Version, repetido.Version);
        Assert.Equal(1, caso.Guardados);
        Assert.Single(caso.Ruta.Escaneos);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("codigo")]
    [InlineData("identificador")]
    [InlineData("despachada")]
    public async Task CargaInvalidaNoRegistraEscaneos(string motivo)
    {
        var caso = new Caso(cargar: false);
        if (motivo == "despachada") caso.Ruta.Despachar(caso.Responsable, Caso.Ahora);
        var handler = new RegistrarCargaRutaHandler(caso, new Reloj());
        var solicitud = new RegistrarCargaRutaCommand(motivo == "codigo" ? "ajeno" : "B-1",
            motivo == "version" ? Guid.NewGuid() : caso.Version, motivo == "identificador" ? Guid.NewGuid() : null);
        Assert.NotNull(await Record.ExceptionAsync(() => handler.Handle(caso.Ruta.Id, solicitud)));
        Assert.Equal(0, caso.Guardados);
        Assert.Empty(caso.Ruta.Escaneos);
    }

    [Fact]
    public async Task CargaRechazaCamposVaciosAntesDeAbrirLaPersistencia()
    {
        var caso = new Caso(false);
        var handler = new RegistrarCargaRutaHandler(caso, new Reloj());
        await Assert.ThrowsAsync<ArgumentException>(() => handler.ConsultarAsync(Guid.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(caso.Ruta.Id, null!));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(caso.Ruta.Id, new("", caso.Version)));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(caso.Ruta.Id, new("B-1", Guid.Empty)));
        Assert.Equal(0, caso.Dispuestos);
    }

    [Fact]
    public async Task ConsultaCargaYDespachaConVersionNuevaYLiberacionDeLaUnidad()
    {
        var caso = new Caso();
        var handler = new DespacharRutaHandler(caso, new Reloj());
        var preparacion = await handler.ConsultarAsync(caso.Ruta.Id);
        Assert.True(Assert.Single(preparacion.Bultos).Cargado);
        Assert.Null(preparacion.DespachadaEn);
        using var cancelacion = new CancellationTokenSource();
        var resultado = await handler.Handle(caso.Ruta.Id, new(preparacion.Version), cancelacion.Token);
        Assert.Equal(Caso.Ahora, resultado.DespachadaEn);
        Assert.Equal(caso.Version, resultado.Version);
        Assert.NotEqual(preparacion.Version, resultado.Version);
        Assert.Equal(EstadoEnvio.EnTransito, caso.Ruta.Paradas.Single().Envio.Estado);
        Assert.Equal(1, caso.Guardados);
        Assert.Equal(2, caso.Dispuestos);
        Assert.Equal(cancelacion.Token, caso.Token);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("carga")]
    [InlineData("inactivo")]
    [InlineData("despachada")]
    public async Task RechazaVersionViejaCargaIncompletaYRepartidorInactivo(string motivo)
    {
        var caso = new Caso(cargar: motivo != "carga");
        var version = motivo == "version" ? Guid.NewGuid() : caso.Version;
        if (motivo == "inactivo") caso.Ruta.Repartidor.Desactivar();
        if (motivo == "despachada") caso.Ruta.Despachar(caso.Responsable, Caso.Ahora);
        var error = await Record.ExceptionAsync(() => new DespacharRutaHandler(caso, new Reloj())
            .Handle(caso.Ruta.Id, new(version)));
        Assert.IsAssignableFrom<InvalidOperationException>(error);
        if (motivo == "version") Assert.IsType<ConflictoConcurrenciaException>(error);
        Assert.Equal(0, caso.Guardados);
        Assert.Equal(1, caso.Dispuestos);
    }

    [Fact]
    public async Task IdentificadoresInvalidosNoAbrenLaPersistencia()
    {
        var caso = new Caso();
        var handler = new DespacharRutaHandler(caso, new Reloj());
        await Assert.ThrowsAsync<ArgumentException>(() => handler.ConsultarAsync(Guid.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(caso.Ruta.Id, null!));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(caso.Ruta.Id, new(Guid.Empty)));
        Assert.Equal(0, caso.Dispuestos);
    }

    private sealed class Caso : IDespachoRutaRepository, IRegistroCargaRutaRepository, IOperacionDespachoRuta
    {
        internal static readonly DateTime Ahora = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);
        public Ruta Ruta { get; }
        public Usuario Responsable { get; }
        public Guid Version { get; private set; } = Guid.NewGuid();
        public int Guardados { get; private set; }
        public int Dispuestos { get; private set; }
        public CancellationToken Token { get; private set; }
        public Caso(bool cargar = true)
        {
            var operador = new Operador("Operador");
            Responsable = new Usuario(operador, "admin@example.com", RolUsuario.Administrador);
            var zona = new ZonaCobertura(operador, "Centro", "11000", [new("Día", new(8, 0), new(18, 0))]);
            var reglas = new ReglasOperativas(operador, 1, Ahora.AddDays(-1), 2, 0, [TipoPrueba.Foto], new(1, 5, true));
            _ = new PlazoServicio(reglas, ModalidadServicio.Estandar, 24, new(16, 0));
            var cuadro = new CuadroTarifario(operador, 1, Ahora.AddDays(-1));
            _ = new LineaTarifa(cuadro, zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
            var envio = new Envio(new CuentaComercial(operador, new Comercio("Comercio", "099123456", "c@example.com")),
                new("Persona", "099123456", "p@example.com", new("Calle", "1", "Montevideo", "11000")),
                [new("B-1", 1, 10, 10, 10)], ModalidadServicio.Estandar, zona, reglas, cuadro, 150,
                Ahora, Ahora.AddHours(1), ProgramacionEntrega.Crear(zona.Franjas.Single(), new(2026, 11, 13), TimeSpan.Zero), Responsable);
            var bulto = envio.Bultos.Single();
            EscaneoBulto.RegistrarRecepcion(bulto, Responsable, Ahora);
            envio.RecibirEnDeposito(Responsable, Ahora);
            Ruta = new(new Repartidor(operador, "repartidor@example.com"), new Vehiculo(operador, "ABC123", 100, 10), 10);
            Ruta.AgregarParada(envio, 1, Ahora.AddHours(1), Responsable, Ahora);
            if (cargar) EscaneoBulto.RegistrarCarga(Ruta, bulto, Responsable, Ahora);
        }
        public Task<IOperacionDespachoRuta> AbrirAsync(Guid id, CancellationToken ct)
        { Token = ct; return Task.FromResult<IOperacionDespachoRuta>(this); }
        public Task<IOperacionDespachoRuta> AbrirCargaAsync(Guid id, CancellationToken ct) => AbrirAsync(id, ct);
        public Task GuardarAsync(CancellationToken ct)
        { Token = ct; Guardados++; Version = Guid.NewGuid(); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Dispuestos++; return ValueTask.CompletedTask; }
    }
    private sealed class Reloj : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Caso.Ahora); }
}
