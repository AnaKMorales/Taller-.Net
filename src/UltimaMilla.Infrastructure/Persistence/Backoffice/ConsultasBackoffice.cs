using UltimaMilla.Application.Backoffice;
using UltimaMilla.Application.Tablero;

namespace UltimaMilla.Infrastructure.Persistence.Backoffice;

// Los filtros de tenant del AppDbContext limitan todo al operador del usuario.
public sealed class ConsultasBackoffice(AppDbContext db, TimeProvider? reloj = null) : IConsultasBackoffice
{
    public static readonly EstadoEnvio[] EstadosConIncidencia =
        [EstadoEnvio.NoEntregado, EstadoEnvio.Reprogramado, EstadoEnvio.EnDevolucion, EstadoEnvio.Extraviado];

    // Envíos que todavía tienen que llegar al destinatario: los que pueden incumplir su ventana.
    // Quedan afuera los terminales y los que ya están volviendo al comercio.
    public static readonly EstadoEnvio[] EstadosPorEntregar =
        [EstadoEnvio.Admitido, EstadoEnvio.EnDeposito, EstadoEnvio.AsignadoARuta, EstadoEnvio.EnTransito,
         EstadoEnvio.NoEntregado, EstadoEnvio.Reprogramado, EstadoEnvio.EntregadoParcialmente];

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    // La ventana vigente es la de la programación actual (si se reprogramó, la nueva). Su fin se guarda como
    // texto con el desfase horario local, así que no se puede comparar en SQL: se acota por fecha en la base
    // y se compara el instante exacto en memoria.
    public async Task<IReadOnlyList<EnvioEnRiesgoDto>> EnviosEnRiesgoAsync(TimeSpan margen,
        CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.GetUtcNow();
        var limite = ahora + margen;
        // Un día de holgura cubre cualquier desfase horario entre la fecha local de entrega y UTC.
        var hastaFecha = DateOnly.FromDateTime(limite.UtcDateTime).AddDays(1);
        var candidatos = await db.Envios.AsNoTracking()
            .Where(e => EstadosPorEntregar.Contains(e.Estado) && e.Programacion.FechaEntrega <= hastaFecha)
            .Select(e => new
            {
                e.Id,
                e.CodigoSeguimiento,
                Destinatario = e.Destinatario.Nombre,
                Zona = e.Zona.Nombre,
                e.Estado,
                e.Programacion.FinLocal,
                Repartidor = e.Parada == null ? null : e.Parada.Ruta.Repartidor.Email
            })
            .ToListAsync(cancellationToken);
        return candidatos.Where(c => c.FinLocal <= limite)
            .OrderBy(c => c.FinLocal)
            .Select(c => new EnvioEnRiesgoDto(c.Id, c.CodigoSeguimiento, c.Destinatario, c.Zona, c.Estado, c.FinLocal,
                c.FinLocal <= ahora, c.Repartidor))
            .ToList();
    }

    public async Task<IReadOnlyList<PosicionRepartidorDto>> UltimasPosicionesAsync(TimeSpan antiguedadMaxima,
        CancellationToken cancellationToken = default)
    {
        var desde = _reloj.GetUtcNow().UtcDateTime - antiguedadMaxima;
        // Solo eventos de la app del repartidor: su responsable es el repartidor y su posición, la del dispositivo.
        var eventos = await db.Set<EventoDeEnvio>().AsNoTracking()
            .Where(e => e.Origen == OrigenEvento.AppRepartidor && e.Posicion != null && e.OcurridoEn >= desde)
            .OrderByDescending(e => e.OcurridoEn)
            .Take(500)
            .Select(e => new { e.Responsable!.Email, Posicion = e.Posicion!, e.OcurridoEn })
            .ToListAsync(cancellationToken);
        // Seguimiento periódico del GPS durante la jornada (req. 23): la PWA lo manda cada pocos minutos.
        var lecturas = await db.Set<PosicionVehiculo>().AsNoTracking()
            .Where(p => p.RegistradaEn >= desde)
            .OrderByDescending(p => p.RegistradaEn)
            .Take(500)
            .Select(p => new { p.Ruta.Repartidor.Email, Posicion = p.Punto, OcurridoEn = p.RegistradaEn })
            .ToListAsync(cancellationToken);
        // Gana la más reciente de las dos fuentes para cada repartidor.
        var ultimas = eventos.Concat(lecturas).GroupBy(e => e.Email)
            .Select(g => g.MaxBy(e => e.OcurridoEn)!).ToList();

        // El vehículo es el de la última ruta despachada del repartidor
        var emails = ultimas.Select(e => e.Email).ToList();
        var vehiculos = (await db.Set<Ruta>().AsNoTracking()
                .Where(r => r.DespachadaEn != null && emails.Contains(r.Repartidor.Email))
                .Select(r => new { r.Repartidor.Email, r.Vehiculo.Matricula, r.DespachadaEn })
                .ToListAsync(cancellationToken))
            .GroupBy(r => r.Email)
            .ToDictionary(g => g.Key, g => g.MaxBy(r => r.DespachadaEn)!.Matricula);

        return ultimas
            .Select(e => new PosicionRepartidorDto(e.Email, e.Posicion.Latitud, e.Posicion.Longitud, e.OcurridoEn,
                vehiculos.GetValueOrDefault(e.Email)))
            .OrderBy(p => p.Repartidor)
            .ToList();
    }

    public async Task<ResumenTablero> TableroAsync(int ultimosCambios, CancellationToken cancellationToken = default)
    {
        var porEstado = await db.Envios.AsNoTracking()
            .GroupBy(e => e.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(g => g.Estado, g => g.Cantidad, cancellationToken);

        var operadorId = db.OperadorIdActual;
        var cambios = await db.Set<EventoDeEnvio>().AsNoTracking()
            .OrderByDescending(e => e.OcurridoEn)
            .Take(ultimosCambios)
            .Select(e => new CambioEnvio(operadorId, e.Envio.Id, e.Tipo, e.EstadoAnterior, e.EstadoNuevo,
                e.OcurridoEn, e.Detalle))
            .ToListAsync(cancellationToken);

        return new ResumenTablero(porEstado, cambios);
    }

    public async Task<IReadOnlyList<IncidenciaDto>> IncidenciasAsync(CancellationToken cancellationToken = default) =>
        await db.Envios.AsNoTracking()
            .Where(e => EstadosConIncidencia.Contains(e.Estado) || e.Discrepancias.Any())
            .OrderByDescending(e => e.Eventos.Max(v => v.OcurridoEn))
            .Select(e => new IncidenciaDto(e.Id, e.Destinatario.Nombre, e.Estado,
                e.Intentos.Count(i => i.Resultado == ResultadoIntento.Fallido),
                e.Intentos.OrderByDescending(i => i.Numero).Select(i => i.MotivoNoEntrega!.Nombre).FirstOrDefault(),
                e.Discrepancias.Count,
                e.Eventos.Max(v => v.OcurridoEn)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<EnvioPorRecibirDto>> PorRecibirAsync(CancellationToken cancellationToken = default) =>
        await db.Envios.AsNoTracking()
            .Where(e => e.Estado == EstadoEnvio.Admitido)
            .OrderBy(e => e.FechaAdmision)
            .Select(e => new EnvioPorRecibirDto(e.Id, e.CodigoSeguimiento, e.CuentaComercial.Comercio.RazonSocial,
                e.Destinatario.Nombre, e.Bultos.Count, e.FechaAdmision))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AvisoFallidoDto>> AvisosFallidosAsync(CancellationToken cancellationToken = default)
    {
        var filas = await db.Set<EntregaAviso>().AsNoTracking()
            .Where(a => !a.Exitoso && a.Intentos > 0)
            .OrderByDescending(a => a.UltimoIntentoEn)
            .Select(a => new
            {
                a.Id,
                EnvioId = a.Evento.Envio.Id,
                Comercio = a.Suscripcion.CuentaComercial.RazonSocial,
                a.Suscripcion.UrlDestino,
                a.Evento.Tipo,
                a.Intentos,
                a.UltimoIntentoEn
            })
            .ToListAsync(cancellationToken);
        // Solo el host: la URL completa puede tener datos del comercio.
        return filas.Select(f => new AvisoFallidoDto(f.Id, f.EnvioId, f.Comercio, f.UrlDestino.Host, f.Tipo,
            f.Intentos, f.UltimoIntentoEn)).ToList();
    }
}
