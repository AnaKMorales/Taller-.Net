using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Envios;

namespace UltimaMilla.Infrastructure.IntegrationTests;

public sealed partial class PostgresTests
{
    [PostgresFact]
    public async Task ElComercioDaDeAltaUnEnvioConTarifaYFechasCalculadasEnElServidor()
    {
        var e = Escenario();
        await using (var db = Context())
        {
            db.Add(e.Envio);
            await db.SaveChangesAsync();
        }
        // 14/10 a las 09:00 locales (12:00 UTC), antes del corte de las 16:00: compromiso el 15/10 a las 09:00.
        var reloj = new RelojFijo(new DateTimeOffset(EscenarioPersistencia.Fecha));
        var solicitud = new SolicitudAltaPortal(e.Zona.Id, ModalidadServicio.Estandar, "Destinataria", "099123456",
            "destinataria@example.com", "Av. Italia", "2500", "Montevideo", "11600",
            [new BultoAlta("CAJA-1", 2.5m, 20, 30, 40), new BultoAlta("CAJA-2", 1.2m, 10, 10, 10)]);

        AltaCreada creada;
        await using (var db = ContextoComercio(e, e.Envio.CuentaComercial.Id))
        {
            var alta = Handler(db, reloj);
            var cotizacion = await alta.CotizarAsync(solicitud);
            // Línea de $150 con 10 % de recargo, sobre 3,7 kg.
            Assert.Equal(165m, cotizacion.Tarifa.Desglose.Total);
            Assert.Equal(new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.FromHours(-3)), cotizacion.FechaComprometida);
            Assert.Equal(new DateOnly(2026, 10, 15), cotizacion.Opciones[0].Fecha);

            creada = await alta.ConfirmarAsync(solicitud, cotizacion.Opciones[0].Clave);
            Assert.Equal(165m, creada.Tarifa);
            // Una opción que no salió de la cotización se rechaza sin crear nada.
            await Assert.ThrowsAsync<ArgumentException>(() => alta.ConfirmarAsync(solicitud, "2026-10-15|" + Guid.NewGuid()));
        }

        await using (var db = Context())
        {
            var envio = await db.Envios.Include(x => x.Bultos).Include(x => x.Programacion).Include(x => x.CuentaComercial)
                .SingleAsync(x => x.CodigoSeguimiento == creada.CodigoSeguimiento);
            Assert.Equal((EstadoEnvio.Admitido, 165m, 2), (envio.Estado, envio.TarifaCalculada, envio.Bultos.Count));
            Assert.Equal(e.Envio.CuentaComercial.Id, envio.CuentaComercialId);
            Assert.Equal(new DateTimeOffset(2026, 10, 15, 18, 0, 0, TimeSpan.FromHours(-3)), envio.Programacion.FinLocal);
            Assert.Equal(2, await db.Envios.CountAsync());
        }

        // Un usuario interno (sin cuenta comercial) no puede usar el alta del portal.
        await using (var interno = Context())
            await Assert.ThrowsAsync<AislamientoOperadorException>(() => Handler(interno, reloj).CotizarAsync(solicitud));
    }

    private AltaEnvioPortalHandler Handler(AppDbContext db, TimeProvider reloj) => new(
        new OpcionesAlta(db, reloj), new TarifarioVigente(Options(), reloj), new CalculadorTarifa(),
        new CrearEnvioHandler(new AltaEnvioRepository(db), reloj), reloj, new OpcionesOperacion());
}
