using UltimaMilla.Repartidor.Contratos;
using UltimaMilla.Repartidor.Sincronizacion;

namespace UltimaMilla.Repartidor.Tests;

// Mismas reglas que Envio.RegistrarIntento que pueden comprobarse sin conexión.
public sealed class ValidadorAccionTests
{
    private readonly HojaDeRutaDto _hoja = Datos.Hoja(EstadoEnvio.EnTransito, EstadoEnvio.Entregado);

    private Guid EnTransito => _hoja.Paradas[0].EnvioId;

    [Fact]
    public void EntregaConTodasLasPruebasEsValida() =>
        Assert.Empty(ValidadorAccion.Validar(Datos.Entrega(EnTransito), _hoja));

    [Fact]
    public void EntregaSinUnaPruebaExigidaEsInvalida()
    {
        var completa = Datos.Entrega(EnTransito);
        var accion = completa with { Pruebas = [completa.Pruebas[0]] };
        Assert.Contains("Falta la prueba exigida por el operador: Documento.", ValidadorAccion.Validar(accion, _hoja));
    }

    [Fact]
    public void LasPruebasNecesitanPosicion()
    {
        var completa = Datos.Entrega(EnTransito);
        var accion = completa with { Pruebas = completa.Pruebas.Select(p => p with { Posicion = null }).ToList() };
        Assert.Contains(ValidadorAccion.Validar(accion, _hoja), e => e.StartsWith("Las pruebas necesitan la ubicación"));
    }

    [Fact]
    public void LaAccionSinPruebasNoNecesitaPosicion() =>
        Assert.Empty(ValidadorAccion.Validar(Datos.Fallo(EnTransito, Datos.Ausente.Id), _hoja));

    [Fact]
    public void EntregaExitosaNoLlevaMotivo()
    {
        var accion = Datos.Entrega(EnTransito) with { MotivoId = Datos.Ausente.Id };
        Assert.Contains("Una entrega exitosa no lleva motivo.", ValidadorAccion.Validar(accion, _hoja));
    }

    [Fact]
    public void ElFalloExigeUnMotivoDelCatalogo()
    {
        Assert.Contains("Elegí un motivo del catálogo.", ValidadorAccion.Validar(Datos.Fallo(EnTransito, null), _hoja));
        Assert.Contains("Elegí un motivo del catálogo.", ValidadorAccion.Validar(Datos.Fallo(EnTransito, Guid.NewGuid()), _hoja));
    }

    [Fact]
    public void ElMotivoQueRequiereEvidenciaExigeUnaPrueba()
    {
        Assert.NotEmpty(ValidadorAccion.Validar(Datos.Fallo(EnTransito, Datos.Rechazo.Id), _hoja));
        var evidencia = new PruebaDto(TipoPrueba.Nombre, "Portero", Datos.Inicio, Datos.Posicion);
        Assert.Empty(ValidadorAccion.Validar(Datos.Fallo(EnTransito, Datos.Rechazo.Id, evidencia), _hoja));
    }

    [Fact]
    public void SoloSeRegistranEnviosEnTransitoDeLaHoja()
    {
        Assert.NotEmpty(ValidadorAccion.Validar(Datos.Entrega(_hoja.Paradas[1].EnvioId), _hoja));
        Assert.Contains("El envío no está en la hoja de ruta.", ValidadorAccion.Validar(Datos.Entrega(Guid.NewGuid()), _hoja));
    }
}
