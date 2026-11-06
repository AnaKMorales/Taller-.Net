using System.Security.Cryptography;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Portal;

namespace UltimaMilla.Infrastructure.Persistence.Backoffice;

// El filtro de cuenta del AppDbContext deja solo las suscripciones y avisos del comercio.
public sealed class PanelAvisos(AppDbContext db) : IPanelAvisos
{
    public async Task<IReadOnlyList<SuscripcionDto>> SuscripcionesAsync(CancellationToken cancellationToken = default)
    {
        if (!db.CuentaRestringida) return [];
        var filas = await db.Set<SuscripcionAviso>().AsNoTracking()
            .OrderByDescending(s => s.Activa)
            .Select(s => new
            {
                s.Id,
                s.UrlDestino,
                s.TiposEvento,
                s.Activa,
                Entregados = s.Entregas.Count(e => e.Exitoso),
                Fallidos = s.Entregas.Count(e => !e.Exitoso && e.Intentos > 0),
                Pendientes = s.Entregas.Count(e => e.Intentos == 0)
            })
            .ToListAsync(cancellationToken);
        return filas.Select(f => new SuscripcionDto(f.Id, f.UrlDestino.AbsoluteUri, f.TiposEvento, f.Activa,
            f.Entregados, f.Fallidos, f.Pendientes)).ToList();
    }

    public async Task<IReadOnlyList<AvisoDto>> UltimosAvisosAsync(int cantidad, CancellationToken cancellationToken = default)
    {
        if (!db.CuentaRestringida) return [];
        return await db.Set<EntregaAviso>().AsNoTracking()
            .OrderByDescending(a => a.Evento.OcurridoEn)
            .Take(cantidad)
            .Select(a => new AvisoDto(a.Id, a.Evento.Envio.CodigoSeguimiento, a.Evento.Tipo,
                a.Exitoso ? EstadoAviso.Entregado : a.Intentos > 0 ? EstadoAviso.Fallido : EstadoAviso.Pendiente,
                a.Intentos, a.Evento.OcurridoEn, a.UltimoIntentoEn))
            .ToListAsync(cancellationToken);
    }

    public async Task<SuscripcionCreada> CrearSuscripcionAsync(Uri destino, IReadOnlyCollection<TipoEvento> eventos,
        CancellationToken cancellationToken = default)
    {
        if (db.CuentaComercialIdActual is not Guid cuentaId) throw new AislamientoOperadorException();
        var cuenta = await db.Set<CuentaComercial>().SingleAsync(c => c.Id == cuentaId, cancellationToken);
        // 32 bytes aleatorios: el comercio lo usa para verificar la firma de cada aviso.
        var secreto = "whsec_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var suscripcion = new SuscripcionAviso(cuenta, eventos, secreto, destino);
        db.Add(suscripcion);
        await db.SaveChangesAsync(cancellationToken);
        return new SuscripcionCreada(suscripcion.Id, secreto);
    }

    public async Task<bool> DesactivarSuscripcionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!db.CuentaRestringida) return false;
        var suscripcion = await db.Set<SuscripcionAviso>().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (suscripcion is null) return false;
        suscripcion.Desactivar();
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
