using UltimaMilla.Application.Backoffice;
using UltimaMilla.Application.Tablero;

namespace UltimaMilla.Infrastructure.Persistence.Backoffice;

// Los filtros de tenant del AppDbContext limitan todo al operador del usuario.
public sealed class ConsultasBackoffice(AppDbContext db) : IConsultasBackoffice
{
    public static readonly EstadoEnvio[] EstadosConIncidencia =
        [EstadoEnvio.NoEntregado, EstadoEnvio.Reprogramado, EstadoEnvio.EnDevolucion, EstadoEnvio.Extraviado];

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

    public async Task<IReadOnlyList<AvisoFallidoDto>> AvisosFallidosAsync(CancellationToken cancellationToken = default)
    {
        var filas = await db.Set<EntregaAviso>().AsNoTracking()
            .Where(a => !a.Exitoso && a.Intentos > 0)
            .OrderByDescending(a => a.UltimoIntentoEn)
            .Select(a => new
            {
                a.Id,
                EnvioId = a.Evento.Envio.Id,
                Comercio = a.Suscripcion.CuentaComercial.Comercio.RazonSocial,
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
