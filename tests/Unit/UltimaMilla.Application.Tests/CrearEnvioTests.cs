using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Domain.Comercios;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Operadores;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Tests;

public sealed class CrearEnvioTests
{
    [Fact]
    public async Task GuardaElEnvioConLaPreparacionDelServidorYElResponsable()
    {
        var caso = new CasoAlta();
        using var cancellation = new CancellationTokenSource();
        var envio = await caso.Handler.Handle(caso.Command, cancellation.Token);

        Assert.Same(envio, caso.Guardado);
        Assert.Equal(cancellation.Token, caso.TokenDeCarga);
        Assert.Equal(cancellation.Token, caso.TokenDeGuardado);
        Assert.Equal(caso.Command.CuentaComercialId, envio.CuentaComercialId);
        Assert.Equal("Destinatario", envio.Destinatario.Nombre);
        Assert.Equal("B-1", Assert.Single(envio.Bultos).Codigo);
        Assert.Equal(CasoAlta.Ahora, envio.FechaAdmision);
        Assert.Equal(caso.Command.Preparacion!.FechaComprometida, envio.FechaComprometida);
        Assert.Equal(caso.Command.Preparacion.InicioLocal, envio.Programacion.InicioLocal);
        Assert.Equal(caso.Command.Preparacion.TarifaCalculada, envio.TarifaCalculada);
        Assert.Equal(caso.Responsable.Id, Assert.Single(envio.Eventos).ResponsableId);
    }

    [Fact]
    public async Task RechazaUnaSolicitudSinBultosAntesDeConsultarElRepositorio()
    {
        var caso = new CasoAlta();
        caso.Command.Bultos.Clear();

        await Assert.ThrowsAsync<ArgumentException>(() => caso.Handler.Handle(caso.Command));
        Assert.False(caso.Cargado);
        Assert.Null(caso.Guardado);
    }

    [Fact]
    public async Task NoGuardaSiLaFranjaNoPerteneceALaZona()
    {
        var caso = new CasoAlta();
        caso.Command.FranjaHorariaId = Guid.NewGuid();

        await Assert.ThrowsAsync<ArgumentException>(() => caso.Handler.Handle(caso.Command));
        Assert.True(caso.Cargado);
        Assert.Null(caso.Guardado);
    }

    [Fact]
    public async Task NoGuardaSiLaFechaComprometidaNoEstaEnUtc()
    {
        var caso = new CasoAlta();
        var preparacion = caso.Command.Preparacion!;
        caso.Command.Preparacion = preparacion with
        {
            FechaComprometida = DateTime.SpecifyKind(preparacion.FechaComprometida, DateTimeKind.Unspecified)
        };

        await Assert.ThrowsAsync<ArgumentException>(() => caso.Handler.Handle(caso.Command));
        Assert.Null(caso.Guardado);
    }

    private sealed class CasoAlta : IAltaEnvioRepository
    {
        internal static readonly DateTime Ahora = new(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc);
        private readonly ReferenciasAltaEnvio _referencias;
        public CrearEnvioCommand Command { get; }
        public Usuario Responsable => _referencias.Responsable;
        public CrearEnvioHandler Handler => new(this, new RelojAlta());
        public Envio? Guardado { get; private set; }
        public bool Cargado { get; private set; }
        public CancellationToken TokenDeCarga { get; private set; }
        public CancellationToken TokenDeGuardado { get; private set; }

        public CasoAlta()
        {
            var operador = new Operador("Operador");
            var comercio = new Comercio("Comercio", "099000000", "comercio@example.com");
            var cuenta = new CuentaComercial(operador, comercio);
            var zona = new ZonaCobertura(operador, "Centro", "11000", [new("Día", new(8, 0), new(18, 0))]);
            var reglas = new ReglasOperativas(operador, 1, Ahora.AddDays(-1), 2, 0, [TipoPrueba.Foto], new(1, 5, true));
            _ = new PlazoServicio(reglas, ModalidadServicio.Estandar, 24, new(16, 0));
            var cuadro = new CuadroTarifario(operador, 1, Ahora.AddDays(-1));
            _ = new LineaTarifa(cuadro, zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
            _referencias = new(cuenta, zona, reglas, cuadro, new(operador, "admin@example.com", RolUsuario.Administrador));
            var franja = Assert.Single(zona.Franjas);
            var programacion = ProgramacionEntrega.Crear(franja, new(2026, 11, 9), TimeSpan.Zero);
            Command = new()
            {
                ComercioId = comercio.Id,
                CuentaComercialId = cuenta.Id,
                ZonaId = zona.Id,
                ReglasId = reglas.Id,
                CuadroTarifarioId = cuadro.Id,
                FranjaHorariaId = franja.Id,
                DestinatarioNombre = "Destinatario",
                DestinatarioTelefono = "099000001",
                DestinatarioEmail = "destino@example.com",
                DireccionCalle = "Calle",
                DireccionNumero = "1",
                DireccionLocalidad = "Montevideo",
                DireccionCodigoPostal = "11000",
                ModalidadServicio = ModalidadServicio.Estandar,
                Bultos = [new("B-1", 1, 10, 10, 10)],
                Preparacion = new(150, Ahora.AddHours(1), programacion.FechaEntrega,
                    programacion.InicioLocal, programacion.FinLocal)
            };
        }

        public Task<ReferenciasAltaEnvio> CargarAsync(CrearEnvioCommand command, CancellationToken cancellationToken)
        {
            Cargado = true;
            TokenDeCarga = cancellationToken;
            return Task.FromResult(_referencias);
        }

        public Task GuardarAsync(Envio envio, CancellationToken cancellationToken)
        {
            Guardado = envio;
            TokenDeGuardado = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class RelojAlta : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(CasoAlta.Ahora);
    }
}
