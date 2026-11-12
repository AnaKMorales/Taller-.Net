using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Tests;

public sealed class AltaEnvioPortalTests
{
    private static readonly TimeSpan Uruguay = TimeSpan.FromHours(-3);
    private static readonly PlazoAltaDto Estandar = new(ModalidadServicio.Estandar, 24, new TimeOnly(16, 0));

    private static readonly ZonaAltaDto Centro = new(Guid.NewGuid(), "Centro", "11000",
    [
        new FranjaAltaDto(Guid.NewGuid(), "Mañana", new TimeOnly(8, 0), new TimeOnly(12, 0)),
        new FranjaAltaDto(Guid.NewGuid(), "Tarde", new TimeOnly(14, 0), new TimeOnly(19, 0))
    ]);

    private static DateTimeOffset Local(int dia, int hora, int minuto = 0) => new(2026, 11, dia, hora, minuto, 0, Uruguay);

    [Fact]
    public void AntesDeLaHoraDeCorteElPlazoCuentaDesdeLaAdmision()
    {
        // Martes 10/11 a las 10:00 locales, plazo de 24 h y corte 16:00: miércoles 11/11 a las 10:00.
        var comprometida = AltaEnvioPortalHandler.FechaComprometida(Local(10, 10).ToUniversalTime(), Estandar, Uruguay);
        Assert.Equal(Local(11, 10), comprometida);
        Assert.Equal(Uruguay, comprometida.Offset);
    }

    [Fact]
    public void DespuesDeLaHoraDeCorteElPlazoCuentaDesdeElDiaSiguiente()
    {
        // 10/11 a las 17:30, pasado el corte: cuenta desde el 11/11 a las 00:00, así que vence el 12/11 a las 00:00.
        Assert.Equal(Local(12, 0), AltaEnvioPortalHandler.FechaComprometida(Local(10, 17, 30), Estandar, Uruguay));
        // Justo en la hora de corte todavía entra en el día.
        Assert.Equal(Local(11, 16), AltaEnvioPortalHandler.FechaComprometida(Local(10, 16), Estandar, Uruguay));
    }

    [Fact]
    public void SoloSeOfrecenFranjasQueTerminanDespuesDelCompromiso()
    {
        // Compromiso el 11/11 a las 10:00: la mañana del 11 (termina 12:00) sirve; nada del 10.
        var opciones = AltaEnvioPortalHandler.OpcionesDeEntrega(Centro, Local(11, 10), Local(10, 10), Uruguay, dias: 2);

        Assert.Equal(["2026-11-11 Mañana", "2026-11-11 Tarde", "2026-11-12 Mañana", "2026-11-12 Tarde"],
            opciones.Select(o => $"{o.Fecha:yyyy-MM-dd} {o.Franja}"));
        Assert.All(opciones, o => Assert.True(o.Hasta >= Local(11, 10)));
        Assert.Equal(new DateTimeOffset(2026, 11, 11, 8, 0, 0, Uruguay), opciones[0].Desde);
        Assert.Equal($"2026-11-11|{Centro.Franjas[0].Id}", opciones[0].Clave);
    }

    [Fact]
    public void UnCompromisoAlFinalDelDiaDejaAfueraLasFranjasQueYaCerraron()
    {
        // Compromiso el 11/11 a las 13:00: la mañana de ese día cerró a las 12:00, queda solo la tarde.
        var opciones = AltaEnvioPortalHandler.OpcionesDeEntrega(Centro, Local(11, 13), Local(10, 13), Uruguay, dias: 1);
        Assert.Equal("Tarde", Assert.Single(opciones).Franja);
    }
}
