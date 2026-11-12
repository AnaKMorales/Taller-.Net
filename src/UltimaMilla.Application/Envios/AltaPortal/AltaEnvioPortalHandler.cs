using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Domain.Envios;

namespace UltimaMilla.Application.Envios.AltaPortal;

// Lo que carga el comercio. Ni la cuenta, ni las reglas, ni el tarifario, ni la tarifa, ni las fechas
// vienen del navegador: los resuelve el servidor (requisitos 7 y 9).
public sealed record SolicitudAltaPortal(
    Guid ZonaId, ModalidadServicio Modalidad,
    string DestinatarioNombre, string DestinatarioTelefono, string DestinatarioEmail,
    string Calle, string Numero, string Localidad, string CodigoPostal,
    IReadOnlyList<BultoAlta> Bultos);

// Una fecha y franja de entrega que cumple el plazo comprometido.
public sealed record OpcionEntrega(DateOnly Fecha, Guid FranjaId, string Franja, DateTimeOffset Desde, DateTimeOffset Hasta)
{
    public string Clave => $"{Fecha:yyyy-MM-dd}|{FranjaId}";
}

public sealed record CotizacionAlta(TarifaCalculada Tarifa, DateTimeOffset FechaComprometida, IReadOnlyList<OpcionEntrega> Opciones);

public sealed record AltaCreada(string CodigoSeguimiento, decimal Tarifa, int VersionTarifario,
    DateTimeOffset FechaComprometida, OpcionEntrega Entrega);

// Desfase horario de la operación. El modelo no guarda el huso de cada operador: se usa uno de la plataforma.
public sealed class OpcionesOperacion
{
    public TimeSpan DesfaseHorario { get; set; } = TimeSpan.FromHours(-3);
    public int DiasDeOpciones { get; set; } = 7;
}

// Alta de un envío desde el portal del comercio, en dos pasos:
// 1. Cotizar: tarifa con el tarifario vigente, fecha comprometida según el plazo de la modalidad y las
//    fechas/franjas que la cumplen.
// 2. Confirmar: repite el cálculo en el servidor (nunca confía en lo que mostró la página) y crea el envío
//    con CrearEnvioHandler, que aplica las reglas del dominio.
public sealed class AltaEnvioPortalHandler(IOpcionesAlta opciones, ITarifarioVigente tarifarios,
    ICalculadorTarifa calculador, CrearEnvioHandler crear, TimeProvider reloj, OpcionesOperacion operacion)
{
    public Task<OpcionesAltaDto?> OpcionesAsync(CancellationToken ct = default) => opciones.ObtenerAsync(ct);

    public async Task<CotizacionAlta> CotizarAsync(SolicitudAltaPortal solicitud, CancellationToken ct = default) =>
        (await PrepararAsync(solicitud, ct)).Cotizacion;

    public async Task<AltaCreada> ConfirmarAsync(SolicitudAltaPortal solicitud, string claveEntrega, CancellationToken ct = default)
    {
        var (datos, zona, cotizacion) = await PrepararAsync(solicitud, ct);
        // La opción elegida tiene que seguir siendo válida ahora (el tiempo pasa entre cotizar y confirmar).
        var entrega = cotizacion.Opciones.SingleOrDefault(o => o.Clave == claveEntrega)
            ?? throw new ArgumentException("La fecha y franja elegidas ya no están disponibles. Volvé a calcular.");

        var comando = new CrearEnvioCommand
        {
            CuentaComercialId = datos.CuentaComercialId,
            ZonaId = zona.Id,
            ReglasId = datos.ReglasId,
            CuadroTarifarioId = cotizacion.Tarifa.CuadroId,
            FranjaHorariaId = entrega.FranjaId,
            DestinatarioNombre = solicitud.DestinatarioNombre,
            DestinatarioTelefono = solicitud.DestinatarioTelefono,
            DestinatarioEmail = solicitud.DestinatarioEmail,
            DireccionCalle = solicitud.Calle,
            DireccionNumero = solicitud.Numero,
            DireccionLocalidad = solicitud.Localidad,
            DireccionCodigoPostal = solicitud.CodigoPostal,
            ModalidadServicio = solicitud.Modalidad,
            Bultos = [.. solicitud.Bultos],
            Preparacion = new PreparacionAltaEnvio(cotizacion.Tarifa.Desglose.Total,
                cotizacion.FechaComprometida.UtcDateTime, entrega.Fecha, entrega.Desde, entrega.Hasta)
        };
        var envio = await crear.Handle(comando, ct);
        return new AltaCreada(envio.CodigoSeguimiento, envio.TarifaCalculada, cotizacion.Tarifa.Version,
            cotizacion.FechaComprometida, entrega);
    }

    private async Task<(OpcionesAltaDto Datos, ZonaAltaDto Zona, CotizacionAlta Cotizacion)> PrepararAsync(
        SolicitudAltaPortal solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var datos = await opciones.ObtenerAsync(ct)
            ?? throw new InvalidOperationException("El operador todavía no tiene reglas de entrega vigentes.");
        var zona = datos.Zonas.SingleOrDefault(z => z.Id == solicitud.ZonaId)
            ?? throw new ArgumentException("Elegí una zona de cobertura del operador.");
        var plazo = datos.Plazos.SingleOrDefault(p => p.Modalidad == solicitud.Modalidad)
            ?? throw new ArgumentException("El operador no ofrece esa modalidad de servicio.");
        if (solicitud.Bultos is not { Count: > 0 })
            throw new ArgumentException("Agregá al menos un bulto.");

        var tarifario = await tarifarios.ObtenerAsync(datos.OperadorId, ct)
            ?? throw new InvalidOperationException("El operador todavía no tiene un cuadro tarifario vigente.");
        var tarifa = calculador.Calcular(tarifario, zona.Id, solicitud.Modalidad, solicitud.Bultos);

        var ahora = reloj.GetUtcNow();
        var comprometida = FechaComprometida(ahora, plazo, operacion.DesfaseHorario);
        var opcionesEntrega = OpcionesDeEntrega(zona, comprometida, ahora, operacion.DesfaseHorario, operacion.DiasDeOpciones);
        if (opcionesEntrega.Count == 0)
            throw new InvalidOperationException("La zona no tiene franjas disponibles en los próximos días.");
        return (datos, zona, new CotizacionAlta(tarifa, comprometida, opcionesEntrega));
    }

    // Fecha comprometida: la admisión más el plazo de la modalidad. Si el alta llega después de la hora de
    // corte, el plazo empieza a contar desde el inicio del día siguiente (hora local de la operación).
    public static DateTimeOffset FechaComprometida(DateTimeOffset admision, PlazoAltaDto plazo, TimeSpan desfase)
    {
        var local = admision.ToOffset(desfase);
        var desde = TimeOnly.FromTimeSpan(local.TimeOfDay) > plazo.HoraCorte
            ? new DateTimeOffset(local.Date.AddDays(1), desfase)
            : local;
        return desde.AddHours(plazo.Horas);
    }

    // Fechas y franjas cuya ventana termina después de la fecha comprometida (el dominio exige
    // FechaComprometida <= fin de la ventana) y todavía no terminó.
    public static IReadOnlyList<OpcionEntrega> OpcionesDeEntrega(ZonaAltaDto zona, DateTimeOffset comprometida,
        DateTimeOffset ahora, TimeSpan desfase, int dias)
    {
        var primerDia = DateOnly.FromDateTime(comprometida.ToOffset(desfase).Date);
        var resultado = new List<OpcionEntrega>();
        for (var d = 0; d < dias; d++)
        {
            var fecha = primerDia.AddDays(d);
            foreach (var f in zona.Franjas.OrderBy(f => f.Desde))
            {
                var desde = new DateTimeOffset(fecha.ToDateTime(f.Desde), desfase);
                var hasta = new DateTimeOffset(fecha.ToDateTime(f.Hasta), desfase);
                if (hasta >= comprometida && hasta > ahora)
                    resultado.Add(new OpcionEntrega(fecha, f.Id, f.Nombre, desde, hasta));
            }
        }
        return resultado;
    }
}
