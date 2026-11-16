using Npgsql;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Despacho;

// Guarda las lecturas periódicas del GPS (req. 23). Repartidor y operador salen del contexto autenticado; una ruta
// que no es suya simplemente no aparece (filtro de operador + condición de repartidor) y la lectura se rechaza.
// Cada lectura es independiente: una inválida no tira el lote, para que la cola offline del dispositivo no se trabe.
public sealed class RegistroPosiciones(DbContextOptions<AppDbContext> options, ITenantActual actual, TimeProvider reloj)
    : IRegistroPosiciones
{
    public async Task<PosicionesRegistradasDto> RegistrarAsync(IReadOnlyList<LecturaPosicionDto> lecturas,
        CancellationToken ct)
    {
        try
        {
            return await IntentarAsync(lecturas, ct);
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
                                              { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Otro envío del mismo lote ganó la carrera: se vuelve a calcular y esas lecturas quedan como repetidas.
            return await IntentarAsync(lecturas, ct);
        }
    }

    private async Task<PosicionesRegistradasDto> IntentarAsync(IReadOnlyList<LecturaPosicionDto> lecturas,
        CancellationToken ct)
    {
        if (actual.Contexto is not { Origen: OrigenTenant.UsuarioAutenticado, Rol: RolUsuario.Repartidor,
                UsuarioId: Guid usuarioId } tenant)
            throw new AislamientoOperadorException();
        await using var db = new AppDbContext(options, new TenantActual(tenant));
        var repartidor = await db.Set<Repartidor>()
            .SingleOrDefaultAsync(r => r.Id == usuarioId && r.Activo && r.Rol == RolUsuario.Repartidor, ct)
            ?? throw new AislamientoOperadorException();

        var rutaIds = lecturas.Select(l => l.RutaId).Distinct().ToList();
        var rutas = await db.Set<Ruta>().Include(r => r.Rendicion)
            .Where(r => rutaIds.Contains(r.Id) && r.Repartidor.Id == repartidor.Id)
            .ToDictionaryAsync(r => r.Id, ct);
        var ids = lecturas.Select(l => l.IdLectura).ToList();
        var existentes = (await db.Set<PosicionVehiculo>().AsNoTracking()
                .Where(p => ids.Contains(p.IdLectura) && p.Ruta.Repartidor.Id == repartidor.Id)
                .Select(p => p.IdLectura).ToListAsync(ct))
            .ToHashSet();

        var ahora = reloj.GetUtcNow().UtcDateTime;
        int aceptadas = 0, repetidas = 0, rechazadas = 0;
        foreach (var lectura in lecturas)
        {
            if (existentes.Contains(lectura.IdLectura)) { repetidas++; continue; }
            if (!rutas.TryGetValue(lectura.RutaId, out var ruta)) { rechazadas++; continue; }
            try
            {
                db.Add(new PosicionVehiculo(ruta, repartidor, lectura.IdLectura,
                    new GeoPunto(lectura.Latitud, lectura.Longitud), lectura.PrecisionMetros, lectura.RegistradaEn, ahora));
                aceptadas++;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            {
                rechazadas++;
            }
        }
        if (aceptadas > 0) await db.SaveChangesAsync(ct);
        return new PosicionesRegistradasDto(aceptadas, repetidas, rechazadas);
    }
}
