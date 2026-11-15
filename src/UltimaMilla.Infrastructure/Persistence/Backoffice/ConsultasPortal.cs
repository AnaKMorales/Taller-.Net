using UltimaMilla.Application.Portal;

namespace UltimaMilla.Infrastructure.Persistence.Backoffice;

public sealed class ConsultasPortal(AppDbContext db) : IConsultasPortal
{
    public async Task<IReadOnlyList<EnvioPortalDto>> EnviosAsync(CancellationToken cancellationToken = default)
    {
        if (!db.CuentaRestringida) return [];
        return await db.Envios.AsNoTracking()
            .OrderByDescending(e => e.FechaAdmision)
            .Select(e => new EnvioPortalDto(e.Id, e.CodigoSeguimiento, e.Destinatario.Nombre, e.Estado, e.FechaAdmision))
            .ToListAsync(cancellationToken);
    }

    public async Task<EtiquetaEnvioDto?> EtiquetaAsync(string codigoSeguimiento, CancellationToken cancellationToken = default)
    {
        // Los filtros globales ya dejan solo los envíos de la cuenta del usuario
        if (!db.CuentaRestringida) return null;
        var fila = await db.Envios.AsNoTracking()
            .Where(e => e.CodigoSeguimiento == codigoSeguimiento)
            .Select(e => new
            {
                e.CodigoSeguimiento,
                Operador = e.CuentaComercial.Operador.RazonSocial,
                Comercio = e.CuentaComercial.Comercio.RazonSocial,
                e.Destinatario.Nombre,
                e.Destinatario.Telefono,
                e.Destinatario.Direccion.Calle,
                e.Destinatario.Direccion.Numero,
                e.Destinatario.Direccion.Localidad,
                e.Destinatario.Direccion.CodigoPostal,
                Zona = e.Zona.Nombre,
                e.ModalidadServicio,
                e.FechaComprometida,
                Bultos = e.Bultos.OrderBy(b => b.Codigo).Select(b => new BultoEtiquetaDto(b.Codigo, b.Peso)).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);
        return fila is null ? null : new(fila.CodigoSeguimiento, fila.Operador, fila.Comercio, fila.Nombre, fila.Telefono,
            fila.Calle, fila.Numero, fila.Localidad, fila.CodigoPostal, fila.Zona, fila.ModalidadServicio,
            fila.FechaComprometida, fila.Bultos);
    }
}
