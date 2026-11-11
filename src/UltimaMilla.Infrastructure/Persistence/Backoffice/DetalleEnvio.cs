using UltimaMilla.Application.Backoffice;

namespace UltimaMilla.Infrastructure.Persistence.Backoffice;

// Lectura del detalle de un envío. Los filtros de tenant del AppDbContext limitan la búsqueda al operador
// del usuario: un envío de otro operador no se encuentra (devuelve null, igual que uno inexistente).
public sealed class DetalleEnvio(AppDbContext db) : IDetalleEnvio
{
    public async Task<DetalleEnvioDto?> ObtenerAsync(Guid envioId, CancellationToken cancellationToken = default)
    {
        var envio = await db.Envios.AsNoTracking().AsSplitQuery()
            .Include(e => e.CuentaComercial).ThenInclude(c => c.Comercio)
            .Include(e => e.Destinatario).ThenInclude(d => d.Direccion)
            .Include(e => e.Zona)
            .Include(e => e.Reglas)
            .Include(e => e.CuadroTarifario)
            .Include(e => e.Programacion)
            .Include(e => e.Bultos)
            .Include(e => e.Parada).ThenInclude(p => p!.Ruta).ThenInclude(r => r.Repartidor)
            .Include(e => e.Devolucion)
            .Include(e => e.Intentos).ThenInclude(i => i.MotivoNoEntrega)
            .Include(e => e.Intentos).ThenInclude(i => i.Pruebas)
            .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Bulto)
            .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Motivo)
            .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Evidencias)
            .Include(e => e.Eventos).ThenInclude(v => v.Responsable)
            .SingleOrDefaultAsync(e => e.Id == envioId, cancellationToken);
        if (envio is null) return null;

        var d = envio.Destinatario;
        return new DetalleEnvioDto(
            envio.Id,
            envio.CodigoSeguimiento,
            envio.Estado,
            envio.ModalidadServicio,
            envio.CuentaComercial.Comercio.RazonSocial,
            d.Nombre,
            d.Telefono,
            $"{d.Direccion.Calle} {d.Direccion.Numero}, {d.Direccion.Localidad} ({d.Direccion.CodigoPostal})",
            envio.Zona.Nombre,
            envio.Programacion.InicioLocal,
            envio.Programacion.FinLocal,
            envio.FechaComprometida,
            envio.TarifaCalculada,
            envio.Reglas.Version,
            envio.CuadroTarifario.Version,
            envio.Reglas.PermiteEntregaParcial,
            envio.Reglas.AccionPendientes,
            envio.Parada?.Ruta.Repartidor.Email,
            envio.Devolucion?.Estado,
            envio.Bultos.OrderBy(b => b.Codigo)
                .Select(b => new BultoDetalleDto(b.Codigo, b.Peso, b.VolumenM3, b.Estado)).ToList(),
            envio.Intentos.OrderBy(i => i.Numero)
                .Select(i => new IntentoDetalleDto(i.Numero, i.OcurridoEn, i.Resultado, i.MotivoNoEntrega?.Nombre,
                    i.Detalles.OrderBy(x => x.Bulto.Codigo)
                        .Select(x => new ResultadoBultoDetalleDto(x.Bulto.Codigo, x.Resultado, x.Motivo?.Nombre, x.Evidencias.Count))
                        .ToList(),
                    i.Pruebas.Select(p => p.TipoPrueba).Distinct().ToList()))
                .ToList(),
            envio.Eventos.OrderByDescending(v => v.OcurridoEn)
                .Select(v => new EventoDetalleDto(v.OcurridoEn, v.Tipo, v.Origen, v.EstadoAnterior, v.EstadoNuevo,
                    v.Responsable.Email, v.Detalle))
                .ToList(),
            SinNuevaVisita(envio));
    }

    // Misma condición que exige Envio.Reprogramar para una nueva visita: que queden intentos y que los bultos no
    // entregados en el último intento tengan un motivo reintentable. La regla vive en el dominio (internal);
    // acá se replica solo para orientar la pantalla, y el núcleo la vuelve a validar al asignar.
    private static string? SinNuevaVisita(Envio envio)
    {
        var ultimo = envio.Intentos.MaxBy(i => i.Numero);
        if (ultimo is null) return null;
        if (envio.Intentos.Count >= envio.Reglas.MaxIntentosEntrega)
            return $"se agotaron los {envio.Reglas.MaxIntentosEntrega} intentos que permiten las reglas";
        var noReintentables = ultimo.Detalles.Count == 0
            ? (ultimo.MotivoNoEntrega is { EsReintentable: false } m ? [m.Nombre] : [])
            : ultimo.Detalles.Where(d => d.Resultado == ResultadoIntento.Fallido && d.Motivo is { EsReintentable: false })
                .Select(d => d.Motivo!.Nombre).Distinct().ToList();
        return noReintentables.Count > 0
            ? $"el motivo «{string.Join("», «", noReintentables)}» no permite reintentar la entrega"
            : null;
    }
}
