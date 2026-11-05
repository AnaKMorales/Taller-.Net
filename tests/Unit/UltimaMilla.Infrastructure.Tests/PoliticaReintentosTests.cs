using System.Text;
using System.Text.Json;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Infrastructure.Mensajeria;

namespace UltimaMilla.Infrastructure.Tests;

public sealed class PoliticaReintentosTests
{
    private static readonly OpcionesRabbitMq Opciones = new() { Usuario = "u", Clave = "c" };

    [Theory]
    [InlineData(0, "avisos.espera.5s", 1)]
    [InlineData(1, "avisos.espera.30s", 2)]
    [InlineData(2, "avisos.espera.2m", 3)]
    public void UnFalloVaALaSiguienteColaDeEsperaYSumaUnIntento(int previos, string cola, int nuevos)
    {
        var d = PoliticaReintentos.Decidir(ResultadoConsumo.Reintentar, previos, Opciones);
        Assert.Equal(AccionFinal.EnviarAEspera, d.Accion);
        Assert.Equal(cola, d.ColaEspera);
        Assert.Equal(nuevos, d.IntentosNuevos);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(1000)]
    public void AgotadosLosIntentosVaALaColaDeMuertos(int previos)
    {
        var d = PoliticaReintentos.Decidir(ResultadoConsumo.Reintentar, previos, Opciones);
        Assert.Equal(AccionFinal.EnviarAMuertos, d.Accion);
        Assert.Null(d.ColaEspera);
        Assert.Equal(ResultadoConsumo.Descartar, d.Efectivo);
    }

    [Fact]
    public void ElRecorridoCompletoEsCincoSegundosTreintaSegundosDosMinutosYDlq()
    {
        var recorrido = new List<string>();
        var intentos = 0;
        while (true)
        {
            var d = PoliticaReintentos.Decidir(ResultadoConsumo.Reintentar, intentos, Opciones);
            recorrido.Add(d.ColaEspera ?? Opciones.ColaMuertos);
            if (d.Accion == AccionFinal.EnviarAMuertos) break;
            intentos = d.IntentosNuevos;
        }
        Assert.Equal(["avisos.espera.5s", "avisos.espera.30s", "avisos.espera.2m", "avisos.dlq"], recorrido);
    }

    [Fact]
    public void ConfirmarYDescartarNoUsanEsperas()
    {
        Assert.Equal(AccionFinal.Confirmar, PoliticaReintentos.Decidir(ResultadoConsumo.Confirmar, 2, Opciones).Accion);
        var descartar = PoliticaReintentos.Decidir(ResultadoConsumo.Descartar, 0, Opciones);
        Assert.Equal(AccionFinal.EnviarAMuertos, descartar.Accion);
        Assert.Null(descartar.ColaEspera);
    }

    [Fact]
    public void UnContadorNegativoSeTrataComoCero()
    {
        var d = PoliticaReintentos.Decidir(ResultadoConsumo.Reintentar, -5, Opciones);
        Assert.Equal("avisos.espera.5s", d.ColaEspera);
        Assert.Equal(1, d.IntentosNuevos);
    }

    [Fact]
    public void LasColasSalenDeLaConfiguracion()
    {
        var o = new OpcionesRabbitMq { Usuario = "u", Clave = "c", ColaAvisos = "otra" };
        Assert.Equal("otra.espera.5s", PoliticaReintentos.Decidir(ResultadoConsumo.Reintentar, 0, o).ColaEspera);
        Assert.Equal("otra.dlq", o.ColaMuertos);
    }

    public static TheoryData<object?, int> Encabezados => new()
    {
        { null, 0 }, { 2, 2 }, { 2L, 2 }, { (short)2, 2 }, { (byte)2, 2 }, { "2", 2 }, { Encoding.UTF8.GetBytes("2"), 2 },
        { "basura", 0 }, { -3, 0 }, { 1.5, 0 }, { long.MaxValue, int.MaxValue },
    };

    [Theory]
    [MemberData(nameof(Encabezados))]
    public void ElContadorSeLeeDeCualquierTipoQueEntregueElBroker(object? valor, int esperado)
    {
        var encabezados = new Dictionary<string, object?> { [ConsumidorRabbitMq.EncabezadoIntentos] = valor };
        Assert.Equal(esperado, PoliticaReintentos.LeerIntentos(encabezados));
    }

    [Fact]
    public void SinEncabezadoElContadorEsCero()
    {
        Assert.Equal(0, PoliticaReintentos.LeerIntentos(null));
        Assert.Equal(0, PoliticaReintentos.LeerIntentos(new Dictionary<string, object?>()));
    }
}

public sealed class LectorFallidosTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string Cuerpo(Guid operador) => JsonSerializer.Serialize(new EventoEnvioV1(Guid.NewGuid(), 1, operador,
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Validar", "Sistema", "Admitido", "EnDeposito", DateTime.UtcNow, null), Json);

    [Fact]
    public void LeeElOperadorYLosDatosParaMostrar()
    {
        var operador = Guid.NewGuid();
        var leido = LectorFallidos.Leer("abc", "envio.evento.v1", Cuerpo(operador), 3, null);
        Assert.NotNull(leido);
        Assert.Equal(operador, leido.Value.OperadorId);
        Assert.Equal("abc", leido.Value.Mensaje.MensajeId);
        Assert.Equal(3, leido.Value.Mensaje.Intentos);
        Assert.Equal("EnDeposito", leido.Value.Mensaje.EstadoNuevo);
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("null")]
    [InlineData("""{"hola":1}""")]
    public void UnContenidoSinOperadorNoSePuedeAtribuirANadie(string cuerpo) =>
        Assert.Null(LectorFallidos.Leer("abc", null, cuerpo, 0, null));

    [Fact]
    public void SinMessageIdNoSePuedeReintentar() =>
        Assert.Null(LectorFallidos.Leer(null, null, Cuerpo(Guid.NewGuid()), 0, null));
}
