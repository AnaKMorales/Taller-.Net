using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Envios.ReprogramacionPublica;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Domain.Comercios;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Operadores;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Tests;

public sealed class FlujosPortalTests
{
    [Fact]
    public async Task ConfirmarRecalculaLaTarifaYGuardaConLaCuentaDelServidor()
    {
        var caso = new Caso();
        var handler = caso.Alta();
        using var cancelacion = new CancellationTokenSource();
        Assert.Equal(caso.Opciones, await handler.OpcionesAsync(cancelacion.Token));
        var cotizacion = await handler.CotizarAsync(caso.Solicitud, cancelacion.Token);
        Assert.Equal(150m, cotizacion.Tarifa.Desglose.Total);
        // El precio cambia entre la consulta y el POST: se confirma el vigente, no el mostrado antes.
        caso.Tarifa = caso.Tarifa! with
        {
            Lineas = [caso.Tarifa.Lineas[0] with { Precio = 180m }]
        };
        var resultado = await handler.ConfirmarAsync(caso.Solicitud, cotizacion.Opciones[0].Clave, cancelacion.Token);
        var envio = Assert.Single(caso.Guardados);
        Assert.Equal(180m, resultado.Tarifa);
        Assert.Equal(180m, envio.TarifaCalculada);
        Assert.Equal(caso.Referencias.Cuenta.Id, envio.CuentaComercialId);
        Assert.Equal(envio.CodigoSeguimiento, resultado.CodigoSeguimiento);
        Assert.Equal(cotizacion.Opciones[0].Desde, envio.Programacion.InicioLocal);
        Assert.Equal(DateTimeKind.Utc, envio.FechaComprometida.Kind);
        Assert.Equal(caso.Referencias.Cuenta.Operador.Id, caso.OperadorConsultado);
        Assert.Equal(cancelacion.Token, caso.UltimoToken);
    }

    [Theory]
    [InlineData("reglas", "reglas")]
    [InlineData("zona", "zona")]
    [InlineData("modalidad", "modalidad")]
    [InlineData("bultos", "bulto")]
    [InlineData("tarifario", "tarifario")]
    [InlineData("franjas", "franjas")]
    public async Task ConfiguracionOSolicitudInvalidaNoCreaEnvios(string fallo, string mensaje)
    {
        var caso = new Caso();
        var solicitud = caso.Solicitud;
        switch (fallo)
        {
            case "reglas": caso.Opciones = null; break;
            case "zona": solicitud = solicitud with { ZonaId = Guid.NewGuid() }; break;
            case "modalidad": solicitud = solicitud with { Modalidad = ModalidadServicio.Urgente }; break;
            case "bultos": solicitud = solicitud with { Bultos = [] }; break;
            case "tarifario": caso.Tarifa = null; break;
            case "franjas": caso.Operacion.DiasDeOpciones = 0; break;
        }
        var error = await Record.ExceptionAsync(() => caso.Alta().CotizarAsync(solicitud));
        Assert.NotNull(error);
        Assert.True(error is ArgumentException or InvalidOperationException);
        Assert.Contains(mensaje, error.Message);
        Assert.Empty(caso.Guardados);
    }

    [Fact]
    public async Task UnaOpcionManipuladaONulaNoGeneraUnAlta()
    {
        var caso = new Caso();
        await Assert.ThrowsAsync<ArgumentNullException>(() => caso.Alta().CotizarAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => caso.Alta().ConfirmarAsync(caso.Solicitud, "fecha-ajena"));
        Assert.Empty(caso.Guardados);
    }

    [Fact]
    public async Task ReprogramarOfreceFechasLocalesYGuardaUnEventoDelDestinatario()
    {
        var caso = new Caso();
        caso.PrepararIntentoFallido();
        var handler = caso.Reprogramacion();
        var opciones = await handler.OpcionesAsync(caso.Envio!.CodigoSeguimiento);
        Assert.NotNull(opciones);
        Assert.True(opciones.Disponible);
        Assert.Equal(7, opciones.Opciones.Count);
        Assert.Equal(new DateOnly(2026, 11, 11), opciones.Opciones[0].Fecha);
        Assert.Equal(TimeSpan.FromHours(-3), opciones.Opciones[0].Desde.Offset);
        using var cancelacion = new CancellationTokenSource();
        var resultado = await handler.ReprogramarAsync(caso.Envio.CodigoSeguimiento,
            opciones.Opciones[0].Clave, cancelacion.Token);
        Assert.Equal(ResultadoReprogramacion.Reprogramado, resultado.Resultado);
        Assert.Equal(1, caso.ReprogramacionesGuardadas);
        Assert.Equal(EstadoEnvio.Reprogramado, caso.Envio.Estado);
        Assert.Equal(opciones.Opciones[0].Fecha, caso.Envio.Programacion.FechaEntrega);
        Assert.Equal(OrigenEvento.Destinatario, caso.Envio.Eventos.Last().Origen);
        Assert.Null(caso.Envio.Eventos.Last().Responsable);
        Assert.Equal(cancelacion.Token, caso.UltimoToken);
    }

    [Fact]
    public async Task TokenDesconocidoYEstadoNoElegibleNoPermitenGuardar()
    {
        var caso = new Caso();
        var handler = caso.Reprogramacion();
        Assert.Null(await handler.OpcionesAsync("inexistente"));
        Assert.Equal(ResultadoReprogramacion.NoEncontrado,
            (await handler.ReprogramarAsync("inexistente", "opcion")).Resultado);
        caso.Envio = caso.CrearEnvio();
        Assert.False((await handler.OpcionesAsync(caso.Envio.CodigoSeguimiento))!.Disponible);
        Assert.Equal(ResultadoReprogramacion.NoPermitido,
            (await handler.ReprogramarAsync(caso.Envio.CodigoSeguimiento, "opcion")).Resultado);
        Assert.Equal(0, caso.ReprogramacionesGuardadas);
    }

    [Fact]
    public async Task OpcionInvalidaYPlazoEntreIntentosNoCambianElEnvio()
    {
        var caso = new Caso(plazoEntreIntentos: 72);
        caso.PrepararIntentoFallido();
        var handler = caso.Reprogramacion();
        var token = caso.Envio!.CodigoSeguimiento;
        Assert.Equal(ResultadoReprogramacion.OpcionInvalida,
            (await handler.ReprogramarAsync(token, "opcion-ajena")).Resultado);
        var opciones = (await handler.OpcionesAsync(token))!;
        var resultado = await handler.ReprogramarAsync(token, opciones.Opciones[0].Clave);
        Assert.Equal(ResultadoReprogramacion.OpcionInvalida, resultado.Resultado);
        Assert.Contains("plazo entre intentos", resultado.Mensaje);
        Assert.Equal(EstadoEnvio.NoEntregado, caso.Envio.Estado);
        Assert.Equal(0, caso.ReprogramacionesGuardadas);
    }

    [Fact]
    public async Task ConflictoAlGuardarSeDevuelveComoConflictoYNoComoExito()
    {
        var caso = new Caso { ConflictoAlGuardar = true };
        caso.PrepararIntentoFallido();
        var handler = caso.Reprogramacion();
        var token = caso.Envio!.CodigoSeguimiento;
        var opcion = (await handler.OpcionesAsync(token))!.Opciones[0];
        Assert.Equal(ResultadoReprogramacion.Conflicto,
            (await handler.ReprogramarAsync(token, opcion.Clave)).Resultado);
        Assert.Equal(0, caso.ReprogramacionesGuardadas);
    }

    // Dobles de los puertos de persistencia. El dominio y el cálculo de tarifas son los reales.
    private sealed class Caso : IOpcionesAlta, ITarifarioVigente, IAltaEnvioRepository, IReprogramacionPublicaRepository
    {
        public static readonly DateTime Ahora = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        public ReferenciasAltaEnvio Referencias { get; }
        public OpcionesAltaDto? Opciones { get; set; }
        public TarifarioDto? Tarifa { get; set; }
        public OpcionesOperacion Operacion { get; } = new();
        public SolicitudAltaPortal Solicitud { get; }
        public List<Envio> Guardados { get; } = [];
        public Envio? Envio { get; set; }
        public int ReprogramacionesGuardadas { get; private set; }
        public bool ConflictoAlGuardar { get; init; }
        public Guid OperadorConsultado { get; private set; }
        public CancellationToken UltimoToken { get; private set; }

        public Caso(int plazoEntreIntentos = 0)
        {
            var operador = new Operador("Operador");
            var cuenta = new CuentaComercial(operador, new Comercio("Comercio", "099123456", "comercio@example.com"));
            var zona = new ZonaCobertura(operador, "Centro", "11000", [new("Día", new(8, 0), new(18, 0))]);
            var reglas = new ReglasOperativas(operador, 1, Ahora.AddDays(-1), 3, plazoEntreIntentos,
                [TipoPrueba.Foto], new(1, 5, true));
            _ = new PlazoServicio(reglas, ModalidadServicio.Estandar, 24, new(16, 0));
            _ = new MotivoNoEntrega(reglas, "Ausente", "No atendió", Responsable.Destinatario, false, true);
            var cuadro = new CuadroTarifario(operador, 1, Ahora.AddDays(-1));
            _ = new LineaTarifa(cuadro, zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
            Referencias = new(cuenta, zona, reglas, cuadro, new(operador, "admin@example.com", RolUsuario.Administrador));
            Opciones = new(operador.Id, cuenta.Id, reglas.Id,
                [new(zona.Id, zona.Nombre, zona.CodigoPostal, zona.Franjas.Select(f => new FranjaAltaDto(f.Id, f.Nombre, f.Desde, f.Hasta)).ToArray())],
                [new(ModalidadServicio.Estandar, 24, new(16, 0))]);
            Tarifa = new(cuadro.Id, 1, cuadro.VigenteDesde, null,
                [new(zona.Id, ModalidadServicio.Estandar, 0, 100, 10, 150, [])]);
            Solicitud = new(zona.Id, ModalidadServicio.Estandar, "Persona", "099123456", "persona@example.com",
                "Calle", "1", "Montevideo", [new("B-1", 1, 10, 10, 10)]);
        }

        public AltaEnvioPortalHandler Alta() => new(this, this, new CalculadorTarifa(), new CrearEnvioHandler(this, new Reloj()), new Reloj(), Operacion);
        public ReprogramarPorDestinatarioHandler Reprogramacion() => new(this, new Reloj());
        public Envio CrearEnvio() => new(Referencias.Cuenta,
            new("Persona", "099123456", "persona@example.com", new("Calle", "1", "Montevideo", "11000")),
            [new("B-1", 1, 10, 10, 10)], ModalidadServicio.Estandar, Referencias.Zona,
            Referencias.Reglas, Referencias.Tarifario, 150, Ahora.AddDays(-1), Ahora.AddDays(-1).AddHours(1),
            ProgramacionEntrega.Crear(Referencias.Zona.Franjas.Single(), new(2026, 11, 9), TimeSpan.FromHours(-3)), Referencias.Responsable);

        public void PrepararIntentoFallido()
        {
            Envio = CrearEnvio();
            var fecha = Ahora.AddDays(-1);
            foreach (var bulto in Envio.Bultos) EscaneoBulto.RegistrarRecepcion(bulto, Referencias.Responsable, fecha);
            Envio.RecibirEnDeposito(Referencias.Responsable, fecha);
            var repartidor = new Repartidor(Referencias.Cuenta.Operador, "repartidor@example.com");
            var ruta = new Ruta(repartidor, new Vehiculo(Referencias.Cuenta.Operador, "ABC1234", 100, 10), 10);
            ruta.AgregarParada(Envio, 1, fecha.AddHours(1), Referencias.Responsable, fecha);
            ruta.Despachar(Referencias.Responsable, fecha);
            Envio.RegistrarIntento(ResultadoIntento.Fallido, Referencias.Reglas.MotivosNoEntrega.Single(), [], repartidor,
                fecha.AddHours(1), Guid.NewGuid());
        }

        public Task<OpcionesAltaDto?> ObtenerAsync(CancellationToken cancellationToken = default)
        { UltimoToken = cancellationToken; return Task.FromResult(Opciones); }
        public Task<TarifarioDto?> ObtenerAsync(Guid operadorId, CancellationToken cancellationToken = default)
        { OperadorConsultado = operadorId; UltimoToken = cancellationToken; return Task.FromResult(Tarifa); }
        public Task<ReferenciasAltaEnvio> CargarAsync(CrearEnvioCommand command, CancellationToken cancellationToken)
        { UltimoToken = cancellationToken; return Task.FromResult(Referencias); }
        Task IAltaEnvioRepository.GuardarAsync(Envio envio, CancellationToken cancellationToken)
        { UltimoToken = cancellationToken; Guardados.Add(envio); return Task.CompletedTask; }
        public Task<Envio?> CargarPorTokenAsync(string token, CancellationToken cancellationToken = default)
        { UltimoToken = cancellationToken; return Task.FromResult(Envio?.CodigoSeguimiento == token ? Envio : null); }
        public Task GuardarAsync(Envio envio, CancellationToken cancellationToken = default)
        {
            UltimoToken = cancellationToken;
            if (ConflictoAlGuardar) throw new ConflictoConcurrenciaException(new InvalidOperationException());
            ReprogramacionesGuardadas++;
            return Task.CompletedTask;
        }
    }

    private sealed class Reloj : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Caso.Ahora);
    }
}
