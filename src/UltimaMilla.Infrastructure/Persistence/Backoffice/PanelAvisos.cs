using System.Security.Cryptography;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Mensajeria;
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
                s.VersionContrato,
                Entregados = s.Entregas.Count(e => e.Exitoso),
                Fallidos = s.Entregas.Count(e => !e.Exitoso && e.Intentos > 0),
                Pendientes = s.Entregas.Count(e => e.Intentos == 0)
            })
            .ToListAsync(cancellationToken);
        return filas.Select(f => new SuscripcionDto(f.Id, f.UrlDestino.AbsoluteUri, f.TiposEvento, f.Activa,
            f.Entregados, f.Fallidos, f.Pendientes, f.VersionContrato)).ToList();
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

    public Task<SuscripcionCreada> CrearSuscripcionAsync(Uri destino, IReadOnlyCollection<TipoEvento> eventos,
        CancellationToken cancellationToken = default) => CrearSuscripcionAsync(destino, eventos, 1, cancellationToken);

    public async Task<SuscripcionCreada> CrearSuscripcionAsync(Uri destino, IReadOnlyCollection<TipoEvento> eventos,
        int versionContrato,
        CancellationToken cancellationToken = default)
    {
        if (db.CuentaComercialIdActual is not Guid cuentaId) throw new AislamientoOperadorException();
        var cuenta = await db.Set<CuentaComercial>().SingleAsync(c => c.Id == cuentaId, cancellationToken);
        // 32 bytes aleatorios: el comercio lo usa para verificar la firma de cada aviso.
        var secreto = "whsec_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var suscripcion = new SuscripcionAviso(cuenta, eventos, secreto, destino, versionContrato);
        db.Add(suscripcion);
        await db.SaveChangesAsync(cancellationToken);
        return new SuscripcionCreada(suscripcion.Id, secreto);
    }

    public async Task<AvisoReenviable?> AvisoParaReenviarAsync(Guid avisoId, CancellationToken cancellationToken = default)
    {
        // El filtro de cuenta deja afuera los avisos de otros comercios: para ellos es como si no existieran.
        if (db.CuentaComercialIdActual is not Guid cuentaId) return null;
        var fila = await db.Set<EntregaAviso>().AsNoTracking().Where(a => a.Id == avisoId)
            .Select(a => new
            {
                a.Exitoso,
                EventoId = a.Evento.Id,
                EnvioId = EF.Property<Guid>(a.Evento, "EnvioKey"),
                a.Evento.Tipo,
                a.Evento.Origen,
                a.Evento.EstadoAnterior,
                a.Evento.EstadoNuevo,
                a.Evento.OcurridoEn,
                a.Evento.IdOperacion
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (fila is null) return null;
        // Mismo contrato V1 que publica el outbox; el operador y la cuenta salen del contexto autenticado.
        var evento = new EventoEnvioV1(Guid.Empty, EventoEnvioV1.VersionContrato, db.OperadorIdActual, cuentaId,
            fila.EnvioId, fila.EventoId, fila.Tipo.ToString(), fila.Origen.ToString(), fila.EstadoAnterior?.ToString(),
            fila.EstadoNuevo.ToString(), fila.OcurridoEn, fila.IdOperacion);
        return new AvisoReenviable(fila.Exitoso, evento);
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
