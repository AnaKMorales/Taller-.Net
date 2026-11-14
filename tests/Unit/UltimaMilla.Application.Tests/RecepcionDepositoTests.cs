using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Deposito;
using UltimaMilla.Domain.Comercios;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Operadores;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Application.Tests;

public sealed class RecepcionDepositoTests
{
    [Fact]
    public async Task EscaneoNormalizadoSeReconoceAlReenviarSinOtraEscritura()
    {
        var caso = new Caso();
        var handler = new RecepcionDepositoHandler(caso, new Reloj());
        var antes = await handler.ConsultarAsync(caso.Envio.Id);
        var primero = await handler.EscanearAsync(caso.Envio.Id, new(" b-1 ", antes.Version));
        var repetido = await handler.EscanearAsync(caso.Envio.Id, new("B-1", antes.Version));
        Assert.False(primero.Repetido);
        Assert.True(repetido.Repetido);
        Assert.Equal(primero.Recepcion.Version, repetido.Recepcion.Version);
        Assert.Equal((2, 1, 1), (repetido.Recepcion.Esperados, repetido.Recepcion.Recibidos, repetido.Recepcion.Faltantes));
        Assert.Single(caso.Envio.Bultos.First().Escaneos);
        Assert.Equal(1, caso.Guardados);
        Assert.Equal(3, caso.Dispuestos);
    }

    [Fact]
    public async Task DiscrepanciaDePesoConservaDeclaracionYSeReconoceSinDuplicar()
    {
        var caso = new Caso();
        var handler = new RecepcionDepositoHandler(caso, new Reloj());
        var bulto = caso.Envio.Bultos.First();
        var solicitud = new RegistrarDiscrepanciaCommand(caso.Version, TipoDiscrepancia.PesoNoCoincide,
            " Peso observado ", " 1 kg ", " 1.5 kg ", bulto.Id);
        var resultado = await handler.RegistrarDiscrepanciaAsync(caso.Envio.Id, solicitud);
        Assert.False(resultado.Repetido);
        Assert.True((await handler.RegistrarDiscrepanciaAsync(caso.Envio.Id, solicitud)).Repetido);
        var discrepancia = Assert.Single(resultado.Recepcion.Discrepancias);
        Assert.Equal(("Peso observado", "1 kg", "1.5 kg"),
            (discrepancia.Detalle, discrepancia.DetalleDeclarado, discrepancia.DetalleRecibido));
        Assert.Equal(1, bulto.Peso);
        Assert.Equal(150, caso.Envio.TarifaCalculada);
        Assert.Equal(1, caso.Guardados);
    }

    [Fact]
    public async Task FaltanteYSobranteNoInventanBultosRecibidosNiPermitenConfirmar()
    {
        var caso = new Caso();
        var handler = new RecepcionDepositoHandler(caso, new Reloj());
        var faltante = await handler.RegistrarDiscrepanciaAsync(caso.Envio.Id, new(caso.Version,
            TipoDiscrepancia.BultoFaltante, "No llegó", "B-1", "No recibido", caso.Envio.Bultos.First().Id));
        await handler.RegistrarDiscrepanciaAsync(caso.Envio.Id, new(faltante.Recepcion.Version,
            TipoDiscrepancia.BultoSobrante, "Código desconocido", "No declarado", "EXTRA"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ConfirmarAsync(caso.Envio.Id, new(caso.Version)));
        Assert.Equal(EstadoEnvio.Admitido, caso.Envio.Estado);
        Assert.All(caso.Envio.Bultos, b => Assert.Empty(b.Escaneos));
        Assert.Equal(2, caso.Envio.Bultos.Count);
        Assert.Equal(2, caso.Guardados);
    }

    [Fact]
    public async Task ConfirmaSoloConTodosLosEscaneosYDespuesCierraLaRecepcion()
    {
        var caso = new Caso();
        var handler = new RecepcionDepositoHandler(caso, new Reloj());
        foreach (var bulto in caso.Envio.Bultos)
            await handler.EscanearAsync(caso.Envio.Id, new(bulto.Codigo, caso.Version));
        var resultado = await handler.ConfirmarAsync(caso.Envio.Id, new(caso.Version));
        Assert.Equal(EstadoEnvio.EnDeposito, resultado.Estado);
        Assert.All(caso.Envio.Bultos, b => Assert.Equal(EstadoBulto.EnDeposito, b.Estado));
        var evento = Assert.Single(caso.Envio.Eventos, e => e.Tipo == TipoEvento.Validar);
        Assert.Equal(OrigenEvento.Deposito, evento.Origen);
        Assert.Equal(caso.Responsable, evento.Responsable);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ConfirmarAsync(caso.Envio.Id, new(caso.Version)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.EscanearAsync(caso.Envio.Id, new("B-1", caso.Version)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.RegistrarDiscrepanciaAsync(caso.Envio.Id,
            new(caso.Version, TipoDiscrepancia.Otro, "Detalle", "A", "B")));
        Assert.Equal(3, caso.Guardados);
    }

    [Fact]
    public async Task VersionViejaNoGuardaEscaneosDiscrepanciasNiConfirmacion()
    {
        var caso = new Caso();
        var handler = new RecepcionDepositoHandler(caso, new Reloj());
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => handler.EscanearAsync(caso.Envio.Id, new("B-1", Guid.NewGuid())));
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => handler.RegistrarDiscrepanciaAsync(caso.Envio.Id,
            new(Guid.NewGuid(), TipoDiscrepancia.Otro, "Detalle", "A", "B")));
        await Assert.ThrowsAsync<ConflictoConcurrenciaException>(() => handler.ConfirmarAsync(caso.Envio.Id, new(Guid.NewGuid())));
        Assert.Equal(0, caso.Guardados);
        Assert.Empty(caso.Envio.Discrepancias);
    }

    [Fact]
    public async Task CodigoDesconocidoYBultoAjenoNoSeAsocianAlEnvio()
    {
        var caso = new Caso();
        var handler = new RecepcionDepositoHandler(caso, new Reloj());
        await Assert.ThrowsAsync<ArgumentException>(() => handler.EscanearAsync(caso.Envio.Id, new("AJENO", caso.Version)));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.RegistrarDiscrepanciaAsync(caso.Envio.Id,
            new(caso.Version, TipoDiscrepancia.Otro, "Detalle", "A", "B", Guid.NewGuid())));
        Assert.Equal(0, caso.Guardados);
    }

    [Fact]
    public async Task SolicitudesInvalidasNoAbrenPersistencia()
    {
        var caso = new Caso();
        var handler = new RecepcionDepositoHandler(caso, new Reloj());
        await Assert.ThrowsAsync<ArgumentException>(() => handler.ConsultarAsync(Guid.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.EscanearAsync(caso.Envio.Id, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.RegistrarDiscrepanciaAsync(caso.Envio.Id, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.ConfirmarAsync(caso.Envio.Id, null!));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.EscanearAsync(caso.Envio.Id, new(" ", caso.Version)));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.EscanearAsync(caso.Envio.Id, new("B-1", Guid.Empty)));
        await Assert.ThrowsAsync<ArgumentException>(() => handler.ConfirmarAsync(caso.Envio.Id, new(Guid.Empty)));
        foreach (var solicitud in new RegistrarDiscrepanciaCommand[] {
            new(caso.Version, (TipoDiscrepancia)99, "D", "A", "B"),
            new(caso.Version, TipoDiscrepancia.Otro, "D", "A", "B", Guid.Empty),
            new(caso.Version, TipoDiscrepancia.Otro, "D", " ", "B"),
            new(caso.Version, TipoDiscrepancia.Otro, "D", "A", new string('x', 2001)) })
            await Assert.ThrowsAsync<ArgumentException>(() => handler.RegistrarDiscrepanciaAsync(caso.Envio.Id, solicitud));
        Assert.Equal(0, caso.Dispuestos);
    }

    private sealed class Caso : IRecepcionDepositoRepository, IOperacionRecepcionDeposito
    {
        internal static readonly DateTime Ahora = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        public Envio Envio { get; }
        public Usuario Responsable { get; }
        public Guid Version { get; private set; } = Guid.NewGuid();
        public int Guardados { get; private set; }
        public int Dispuestos { get; private set; }
        public Caso()
        {
            var operador = new Operador("Operador");
            Responsable = new Usuario(operador, "deposito@example.com", RolUsuario.OperarioDeposito);
            var zona = new ZonaCobertura(operador, "Centro", "11000", [new("Día", new(8, 0), new(18, 0))]);
            var reglas = new ReglasOperativas(operador, 1, Ahora.AddDays(-1), 2, 0, [TipoPrueba.Foto], new(1, 5, true));
            _ = new PlazoServicio(reglas, ModalidadServicio.Estandar, 24, new(16, 0));
            var cuadro = new CuadroTarifario(operador, 1, Ahora.AddDays(-1));
            _ = new LineaTarifa(cuadro, zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
            Envio = new(new CuentaComercial(operador, new Comercio("Comercio", "099123456", "c@example.com")),
                new("Persona", "099123456", "p@example.com", new("Calle", "1", "Montevideo", "11000")),
                [new("B-1", 1, 10, 10, 10), new("B-2", 1, 10, 10, 10)], ModalidadServicio.Estandar, zona, reglas, cuadro,
                150, Ahora, Ahora.AddHours(1), ProgramacionEntrega.Crear(zona.Franjas.Single(), new(2026, 11, 14), TimeSpan.Zero), Responsable);
        }
        public Task<IOperacionRecepcionDeposito> AbrirAsync(Guid id, CancellationToken ct) => Task.FromResult<IOperacionRecepcionDeposito>(this);
        public Task GuardarAsync(CancellationToken ct) { Guardados++; Version = Guid.NewGuid(); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Dispuestos++; return ValueTask.CompletedTask; }
    }
    private sealed class Reloj : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Caso.Ahora); }
}
