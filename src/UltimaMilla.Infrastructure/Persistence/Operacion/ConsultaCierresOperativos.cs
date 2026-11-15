using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Application.Tenancy;

namespace UltimaMilla.Infrastructure.Persistence.Operacion;

public sealed class ConsultaCierresOperativos(DbContextOptions<AppDbContext> opciones, ITenantActual actual) : ICierresOperativos
{
    public async Task<IReadOnlyList<CierreOperativoDto>> ConsultarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        if (hasta < desde || hasta.DayNumber - desde.DayNumber >= 366) throw new ArgumentException("El período debe contener entre 1 y 366 días.");
        if (actual.Contexto is not { Origen: OrigenTenant.UsuarioAutenticado, Rol: RolUsuario.Administrador or RolUsuario.Despachador } tenant)
            throw new AislamientoOperadorException();
        await using var db = new AppDbContext(opciones, new TenantActual(tenant));
        if (!await db.Set<Usuario>().AnyAsync(u => u.Id == tenant.UsuarioId && u.Activo && u.Rol == tenant.Rol, ct))
            throw new AislamientoOperadorException();
        var filas = await db.Set<ResumenOperativoDiario>().AsNoTracking().Where(x => x.Dia >= desde && x.Dia <= hasta)
            .OrderByDescending(x => x.Dia).ToListAsync(ct);
        return filas.Select(x => new CierreOperativoDto(x.Dia, x.ActualizadoEn, x.CerradoEn, x.Recalculos, x.VersionCalculo,
            JsonSerializer.Deserialize<IndicadoresDia>(x.ActualJson, ProcesamientoOperativo.Json)!,
            x.AlCierreJson is null ? null : JsonSerializer.Deserialize<IndicadoresDia>(x.AlCierreJson, ProcesamientoOperativo.Json))).ToArray();
    }
}
