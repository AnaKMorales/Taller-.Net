using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.Extravio;
using UltimaMilla.Domain.Comercios;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Operadores;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Tests;

public sealed class RegistrarExtravioTests
{
    [Fact]
    public async Task RegistraSinRutaYConsultaNoModificaElEnvio()
    {
        var caso = new Caso(); var handler = new RegistrarExtravioHandler(caso, new Reloj());
        var consulta = await handler.ConsultarAsync(caso.Envio.Id);
        Assert.Equal(EstadoEnvio.EnDeposito, consulta.Estado); Assert.Equal(0, caso.Guardados);
        var resultado = await handler.Handle(caso.Envio.Id, new(caso.VersionEnvio, null, " No encontrado "));
        Assert.Equal(EstadoEnvio.Extraviado, resultado.Estado); Assert.Null(resultado.RutaAfectadaId);
        Assert.All(resultado.Bultos, b => Assert.Equal(EstadoBulto.Extraviado, b.Estado));
        Assert.Equal(1, caso.Guardados); Assert.Equal(2, caso.Dispuestos);
        Assert.Equal(resultado.EventoId, caso.Envio.Eventos.Last().Id);
        Assert.Equal("No encontrado", caso.Envio.Eventos.Last().Detalle);
    }

    [Fact]
    public async Task ExtravíoConRutaLiberaParadaYDevuelveLasVersionesConfirmadas()
    {
        var caso = new Caso(true); var handler = new RegistrarExtravioHandler(caso, new Reloj());
        var consulta = await handler.ConsultarAsync(caso.Envio.Id); var parada = caso.Envio.Parada!;
        var resultado = await handler.Handle(caso.Envio.Id, new(consulta.VersionEnvio, consulta.VersionRuta, "Perdido"));
        Assert.Null(caso.Envio.Parada); Assert.False(parada.Activa);
        Assert.Equal(caso.Ruta!.Id, resultado.RutaAfectadaId); Assert.NotEqual(consulta.VersionRuta, resultado.VersionRuta);
        Assert.NotEqual(consulta.VersionEnvio, resultado.VersionEnvio);
    }

    [Theory]
    [InlineData("envio-viejo")]
    [InlineData("ruta-vieja")]
    [InlineData("ruta-inesperada")]
    public async Task ConflictosNoModificanEstadoNiHistorial(string motivo)
    {
        var caso = new Caso(motivo != "ruta-inesperada");
        var solicitud = new RegistrarExtravioCommand(motivo == "envio-viejo" ? Guid.NewGuid() : caso.VersionEnvio,
            motivo == "envio-viejo" ? caso.VersionRuta : Guid.NewGuid(), "Detalle");
        var estado = caso.Envio.Estado; var eventos = caso.Envio.Eventos.Count;
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => new RegistrarExtravioHandler(caso, new Reloj()).Handle(caso.Envio.Id, solicitud));
        Assert.Equal(estado, caso.Envio.Estado); Assert.Equal(eventos, caso.Envio.Eventos.Count); Assert.Equal(0, caso.Guardados);
    }

    [Theory]
    [InlineData("sin-detalle")]
    [InlineData("detalle-largo")]
    [InlineData("sin-version")]
    [InlineData("ruta-vacia")]
    [InlineData("ruta-sin-version")]
    [InlineData("sin-id")]
    public async Task SolicitudesInvalidasNoSeGuardan(string motivo)
    {
        var caso = new Caso(motivo == "ruta-sin-version");
        var solicitud = new RegistrarExtravioCommand(motivo == "sin-version" ? Guid.Empty : caso.VersionEnvio,
            motivo == "ruta-vacia" ? Guid.Empty : null, motivo == "sin-detalle" ? " " : motivo == "detalle-largo" ? new string('x', 2001) : "Detalle");
        await Assert.ThrowsAsync<ArgumentException>(() => new RegistrarExtravioHandler(caso, new Reloj()).Handle(
            motivo == "sin-id" ? Guid.Empty : caso.Envio.Id, solicitud));
        Assert.Equal(0, caso.Guardados);
    }

    [Fact]
    public async Task RelojAnteriorAlHistorialNoGeneraUnEventoFueraDeOrden()
    {
        var caso = new Caso();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new RegistrarExtravioHandler(caso, new Reloj(true)).Handle(
            caso.Envio.Id, new(caso.VersionEnvio, null, "Detalle")));
        Assert.Equal(EstadoEnvio.EnDeposito, caso.Envio.Estado); Assert.Equal(0, caso.Guardados);
    }

    private sealed class Caso : IExtravioRepository, IOperacionExtravio
    {
        internal static readonly DateTime Ahora = new(2026, 11, 14, 8, 0, 0, DateTimeKind.Utc);
        public Envio Envio { get; }
        public Ruta? Ruta { get; }
        public Usuario Responsable { get; }
        public Guid VersionEnvio { get; private set; } = Guid.NewGuid();
        public Guid? VersionRuta { get; private set; }
        public int Guardados, Dispuestos;
        public Caso(bool asignar = false)
        {
            var op = new Operador("Operador"); Responsable = new Usuario(op, "a@example.com", RolUsuario.Administrador);
            var cuenta = new CuentaComercial(op, new Comercio("Comercio", "123", "c@example.com"));
            var zona = new ZonaCobertura(op, "Centro", "11000", [new FranjaHoraria("Día", new(8, 0), new(18, 0))]);
            var reglas = new ReglasOperativas(op, 1, Ahora.AddDays(-1), 2, 0, [TipoPrueba.Foto], new(1, 5, true));
            _ = new PlazoServicio(reglas, ModalidadServicio.Estandar, 24, new(16, 0));
            var cuadro = new CuadroTarifario(op, 1, Ahora.AddDays(-1));
            _ = new LineaTarifa(cuadro, zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
            Envio = new(cuenta, new Destinatario("Persona", "123", "p@example.com", new("Calle", "1", "Ciudad", "11000")),
                [new Bulto("B-1", 1, 10, 10, 10)], ModalidadServicio.Estandar, zona, reglas, cuadro, 150, Ahora, Ahora.AddHours(1),
                ProgramacionEntrega.Crear(zona.Franjas.Single(), new(2026, 11, 14), TimeSpan.Zero), Responsable);
            foreach (var b in Envio.Bultos) EscaneoBulto.RegistrarRecepcion(b, Responsable, Ahora);
            Envio.RecibirEnDeposito(Responsable, Ahora);
            if (asignar)
            {
                Ruta = new(new Repartidor(op, "r@example.com"), new Vehiculo(op, "ABC123", 100, 10), 10);
                Ruta.AgregarParada(Envio, 1, Ahora.AddHours(1), Responsable, Ahora); VersionRuta = Guid.NewGuid();
            }
        }
        public Task<IOperacionExtravio> AbrirAsync(Guid id, CancellationToken ct) => Task.FromResult<IOperacionExtravio>(this);
        public Task GuardarAsync(CancellationToken ct)
        { Guardados++; VersionEnvio = Guid.NewGuid(); if (Ruta is not null) VersionRuta = Guid.NewGuid(); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Dispuestos++; return ValueTask.CompletedTask; }
    }
    private sealed class Reloj(bool anterior = false) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(anterior ? Caso.Ahora.AddSeconds(-1) : Caso.Ahora); }
}
