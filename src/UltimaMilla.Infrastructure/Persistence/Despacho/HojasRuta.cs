using System.Data;
using Microsoft.Extensions.DependencyInjection;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Despacho;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Despacho;

public sealed class HojasRuta(DbContextOptions<AppDbContext> options, ITenantActual actual) : IHojasRuta
{
    private async Task<AppDbContext> Abrir(CancellationToken ct)
    {
        var tenant = actual.Contexto;
        if (tenant is not { Origen: OrigenTenant.UsuarioAutenticado, Rol: RolUsuario.Repartidor, UsuarioId: not null })
            throw new AislamientoOperadorException();
        var db = new AppDbContext(options, new TenantActual(tenant));
        try
        {
            if (!await db.Set<Repartidor>().AnyAsync(r => r.Id == tenant.UsuarioId && r.Activo && r.Rol == RolUsuario.Repartidor, ct))
                throw new AislamientoOperadorException();
            return db;
        }
        catch { await db.DisposeAsync(); throw; }
    }

    private static IQueryable<Ruta> Disponibles(AppDbContext db) => db.Set<Ruta>()
        .Where(r => r.Repartidor.Id == db.UsuarioIdActual && r.DespachadaEn != null && r.Rendicion == null);

    public async Task<IReadOnlyList<RutaDisponibleDto>> ListarAsync(CancellationToken ct = default)
    {
        await using var db = await Abrir(ct);
        return await Disponibles(db).AsNoTracking().OrderBy(r => r.DespachadaEn).ThenBy(r => r.Id)
            .Select(r => new RutaDisponibleDto(r.Id, EF.Property<Guid>(r, "RowVersion"), r.DespachadaEn!.Value,
                r.Vehiculo.Matricula, r.Paradas.Count(p => p.Activa))).ToListAsync(ct);
    }

    public async Task<HojaRutaDto?> ObtenerAsync(Guid rutaId, CancellationToken ct = default)
    {
        if (rutaId == Guid.Empty) throw new ArgumentException("La ruta debe tener un identificador válido.", nameof(rutaId));
        await using var db = await Abrir(ct);
        // Una descarga debe conservar versiones y composición del mismo snapshot.
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var ruta = await Disponibles(db).AsSplitQuery()
            .Include(r => r.Vehiculo)
            .Include(r => r.Paradas.Where(p => p.Activa)).ThenInclude(p => p.Bultos).ThenInclude(b => b.Bulto)
            .Include(r => r.Paradas.Where(p => p.Activa)).ThenInclude(p => p.Envio).ThenInclude(e => e.Destinatario).ThenInclude(d => d.Direccion)
            .Include(r => r.Paradas.Where(p => p.Activa)).ThenInclude(p => p.Envio).ThenInclude(e => e.Programacion)
            .Include(r => r.Paradas.Where(p => p.Activa)).ThenInclude(p => p.Envio).ThenInclude(e => e.Reglas).ThenInclude(r => r.MotivosNoEntrega)
            .SingleOrDefaultAsync(r => r.Id == rutaId, ct);
        if (ruta is null) { await snapshot.CommitAsync(ct); return null; }
        var paradas = ruta.Paradas.OrderBy(p => p.Cod).Select(p =>
        {
            var e = p.Envio;
            var d = e.Destinatario;
            var reglas = e.Reglas;
            return new ParadaHojaDto(p.Id, p.Cod, p.HoraEstimadaLlegada, e.Id,
                db.Entry(e).Property<Guid>("RowVersion").CurrentValue, e.Estado, e.VentanaDesde, e.VentanaHasta,
                new(d.Nombre, d.Telefono, new(d.Direccion.Calle, d.Direccion.Numero, d.Direccion.Localidad, d.Direccion.CodigoPostal)),
                new(reglas.Id, reglas.MaxIntentosEntrega, reglas.PlazoEntreIntentosHoras, reglas.TiposPrueba.Order().ToArray(),
                    reglas.MotivosNoEntrega.Where(m => m.Activo).OrderBy(m => m.Nombre).ThenBy(m => m.Id)
                        .Select(m => new MotivoHojaDto(m.Id, m.Nombre, m.Descripcion, m.RequiereEvidencia, m.EsReintentable)).ToArray(),
                    reglas.PermiteEntregaParcial, reglas.AccionPendientes, reglas.ImporteRecargoNuevaVisita),
                p.Bultos.OrderBy(b => b.Bulto.Codigo).ThenBy(b => b.Bulto.Id)
                    .Select(b => new BultoHojaDto(b.Bulto.Id, b.Bulto.Codigo, b.Bulto.Estado, b.PesoKg, b.VolumenM3)).ToArray());
        }).ToArray();
        var resultado = new HojaRutaDto(ruta.Id, db.Entry(ruta).Property<Guid>("RowVersion").CurrentValue,
            ruta.DespachadaEn!.Value, new(ruta.Vehiculo.Matricula, ruta.Vehiculo.CapacidadPesoKg, ruta.Vehiculo.CapacidadVolumenM3), paradas);
        await snapshot.CommitAsync(ct);
        return resultado;
    }
}

public static class HojasRutaServiceCollectionExtensions
{
    public static IServiceCollection AgregarHojasRuta(this IServiceCollection services)
    {
        services.AddScoped<IHojasRuta, HojasRuta>();
        return services;
    }
}
