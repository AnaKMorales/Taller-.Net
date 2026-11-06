using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.RegistrarIntento;
using UltimaMilla.Domain.Comercios;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Operadores;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Tests;

public sealed class RegistrarIntentoTests
{
    [Fact]
    public async Task CoordinaDominioComparteEvidenciaYDevuelveLaVersionGuardada()
    {
        var e = new Caso();
        var resultado = await new RegistrarIntentoHandler(e).Handle(e.Command);
        Assert.Equal(EstadoEnvio.EntregadoParcialmente, resultado.EstadoActualEnvio);
        Assert.Equal(ResultadoIntento.Parcial, resultado.Resultado);
        Assert.Equal(e.Version, resultado.VersionActualEnvio);
        Assert.NotEqual(e.Command.VersionEsperada, resultado.VersionActualEnvio);
        Assert.False(resultado.Repetido);
        Assert.Equal(1, e.Guardados);
        Assert.Equal(1, e.Dispuestos);
        Assert.Equal(e.Actor.Id, e.Envio.Eventos.Last().ResponsableId);
        Assert.Equal(2, resultado.Bultos.Count);
    }

    [Fact]
    public async Task VersionViejaNoInvocaDominioNiGuardaYLiberaLaOperacion()
    {
        var e = new Caso();
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => new RegistrarIntentoHandler(e)
            .Handle(e.Command with { VersionEsperada = Guid.NewGuid() }));
        Assert.Empty(e.Envio.Intentos);
        Assert.Equal(EstadoEnvio.EnTransito, e.Envio.Estado);
        Assert.Equal(0, e.Guardados);
        Assert.Equal(1, e.Dispuestos);
    }

    [Fact]
    public async Task ReenvioIdenticoConVersionOriginalNoVuelveAGuardar()
    {
        var e = new Caso();
        var handler = new RegistrarIntentoHandler(e);
        var primero = await handler.Handle(e.Command);
        var segundo = await handler.Handle(e.Command);
        Assert.True(segundo.Repetido);
        Assert.Equal(primero.IntentoId, segundo.IntentoId);
        Assert.Equal(primero.VersionActualEnvio, segundo.VersionActualEnvio);
        Assert.Equal(1, e.Guardados);
        Assert.Equal(2, e.Dispuestos);
    }

    [Fact]
    public async Task MismoIdentificadorConOtroContenidoNoSeAceptaAunqueElIntentoExista()
    {
        var e = new Caso();
        var handler = new RegistrarIntentoHandler(e);
        await handler.Handle(e.Command);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(e.Command with
        {
            Evidencias = [e.Command.Evidencias[0] with { Archivo = "otra-foto.jpg" }]
        }));
        Assert.Equal(1, e.Guardados);
        Assert.Equal(2, e.Dispuestos);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("referencia")]
    [InlineData("sobrante")]
    [InlineData("duplicada")]
    public async Task SolicitudInvalidaNoAbreLaPersistencia(string caso)
    {
        var e = new Caso();
        var command = caso switch
        {
            "version" => e.Command with { VersionEsperada = Guid.Empty },
            "referencia" => e.Command with { Evidencias = [] },
            "sobrante" => e.Command with { Evidencias = [.. e.Command.Evidencias, e.Command.Evidencias[0] with { Referencia = Guid.NewGuid() }] },
            _ => e.Command with { Evidencias = [e.Command.Evidencias[0], e.Command.Evidencias[0]] }
        };
        await Assert.ThrowsAsync<ArgumentException>(() => new RegistrarIntentoHandler(e).Handle(command));
        Assert.Equal(0, e.Abiertos);
    }

    [Fact]
    public async Task MotivoFueraDelCatalogoNoGuardaYLiberaLaOperacion()
    {
        var e = new Caso();
        var command = e.Command with { Bultos = [e.Command.Bultos[0], e.Command.Bultos[1] with { MotivoId = Guid.NewGuid() }] };
        await Assert.ThrowsAsync<ArgumentException>(() => new RegistrarIntentoHandler(e).Handle(command));
        Assert.Empty(e.Envio.Intentos);
        Assert.Equal(0, e.Guardados);
        Assert.Equal(1, e.Dispuestos);
    }

    [Fact]
    public async Task CancelacionPreviaNoAbrePersistencia()
    {
        var e = new Caso();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RegistrarIntentoHandler(e)
            .Handle(e.Command, new CancellationToken(true)));
        Assert.Equal(0, e.Abiertos);
    }

    private sealed class Caso : IRegistroIntentoRepository
    {
        public Envio Envio { get; }
        public Repartidor Actor { get; }
        public RegistrarIntentoCommand Command { get; }
        public Guid Version = Guid.NewGuid();
        public int Abiertos, Guardados, Dispuestos;

        public Caso()
        {
            var fecha = new DateTime(2026, 11, 3, 8, 0, 0, DateTimeKind.Utc);
            var op = new Operador("Operador");
            var admin = new Usuario(op, "admin@example.com", RolUsuario.Administrador);
            Actor = new Repartidor(op, "rep@example.com");
            var cuenta = new CuentaComercial(op, new Comercio("Comercio", "099000000", "comercio@example.com"));
            var zona = new ZonaCobertura(op, "Centro", "11000", [new("Día", new(8, 0), new(18, 0))]);
            var reglas = new ReglasOperativas(op, 1, fecha.AddDays(-1), 2, 0, [TipoPrueba.Foto], new(1, 5, true), true);
            _ = new PlazoServicio(reglas, ModalidadServicio.Estandar, 24, new(16, 0));
            var motivo = new MotivoNoEntrega(reglas, "Ausente", "Ausente", Responsable.Destinatario, false, true);
            var cuadro = new CuadroTarifario(op, 1, fecha.AddDays(-1));
            _ = new LineaTarifa(cuadro, zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
            var bultos = new[] { new Bulto("A", 1, 10, 10, 10), new Bulto("B", 1, 10, 10, 10) };
            Envio = new(cuenta, new("Persona", "099000000", "persona@example.com", new("Calle", "1", "Montevideo", "11000")),
                bultos, ModalidadServicio.Estandar, zona, reglas, cuadro, 150, fecha, fecha.AddHours(1),
                ProgramacionEntrega.Crear(zona.Franjas.Single(), new(2026, 11, 3), TimeSpan.Zero), admin);
            foreach (var b in bultos) Escanear(b);
            Envio.RecibirEnDeposito(admin, fecha);
            var ruta = new Ruta(Actor, new Vehiculo(op, "ABC123", 100, 10), 10);
            var parada = ruta.AgregarParada(Envio, 1, fecha.AddHours(2), admin, fecha);
            ruta.Despachar(admin, fecha);
            var referencia = Guid.NewGuid();
            Command = new(Envio.Id, parada.Id, Version, Guid.NewGuid(), fecha.AddHours(2),
                [new(bultos[0].Id, ResultadoIntento.Entregado, null, [referencia]), new(bultos[1].Id, ResultadoIntento.Fallido, motivo.Id, [])],
                [new(referencia, TipoPrueba.Foto, fecha.AddHours(2), new GeoPunto(-34.9m, -56.16m), "foto.jpg")]);
            void Escanear(Bulto b) => UltimaMilla.Domain.Deposito.EscaneoBulto.RegistrarRecepcion(b, admin, fecha);
        }

        public Task<IOperacionRegistroIntento> AbrirAsync(Guid envioId, Guid paradaId, Guid idOperacion, CancellationToken ct)
        { Abiertos++; return Task.FromResult<IOperacionRegistroIntento>(new Operacion(this)); }

        private sealed class Operacion(Caso caso) : IOperacionRegistroIntento
        {
            public Envio Envio => caso.Envio;
            public Repartidor Responsable => caso.Actor;
            public Guid VersionActual => caso.Version;
            public Task GuardarAsync(CancellationToken ct)
            { caso.Guardados++; caso.Version = Guid.NewGuid(); return Task.CompletedTask; }
            public ValueTask DisposeAsync() { caso.Dispuestos++; return ValueTask.CompletedTask; }
        }
    }
}
