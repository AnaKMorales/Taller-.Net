using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Infrastructure.Persistence.Backoffice;

namespace UltimaMilla.Infrastructure.Persistence.Operacion;

public sealed class ProcesamientoOperativo(DbContextOptions<AppDbContext> opciones, TimeProvider reloj) : IProcesamientoOperativo
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<Guid>> ListarOperadoresAsync(CancellationToken ct = default)
    {
        await using var db = new AppDbContext(opciones);
        // Enumeración privilegiada mínima; el procesamiento posterior nunca comparte contexto entre operadores.
        return await db.Database.SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM \"Operadores\" ORDER BY \"Id\"").ToListAsync(ct);
    }

    public async Task<ResultadoRecalculo> ProcesarAsync(Guid operadorId, int maximoDias, CancellationToken ct = default)
    {
        if (operadorId == Guid.Empty) throw new ArgumentException("El operador es obligatorio.");
        var hoy = CalendarioOperativo.Hoy(reloj);
        IReadOnlyList<DateOnly> fechas;
        await using (var db = Abrir(operadorId))
        {
            if (!await db.Set<Operador>().AnyAsync(ct)) throw new ArgumentException("El operador no existe.");
            var primera = await db.Envios.Select(e => (DateTime?)e.FechaAdmision).MinAsync(ct);
            var inicio = primera is { } f ? DateOnly.FromDateTime(f + CalendarioOperativo.Desfase) : hoy.AddDays(-1);
            var anteriores = await db.Set<ResumenOperativoDiario>().AsNoTracking().Select(x => new { x.Dia, x.ActualizadoEn })
                .ToDictionaryAsync(x => x.Dia, x => x.ActualizadoEn, ct);
            fechas = CalendarioOperativo.Planificar(inicio, hoy, maximoDias, anteriores);
        }
        var actualizados = 0; var cerrados = 0; var omitidos = 0;
        foreach (var dia in fechas)
        {
            ct.ThrowIfCancellationRequested();
            await using var db = Abrir(operadorId);
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var clave = $"cierre-operativo:{operadorId:N}:{dia:yyyy-MM-dd}";
            var adquirido = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock(hashtextextended({clave}, 0)) AS \"Value\"").SingleAsync(ct);
            if (!adquirido) { omitidos++; continue; }
            var reporte = await new ReportesGestion(db).GenerarAsync(dia, dia, CalendarioOperativo.Desfase, ct);
            var desde = DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.MinValue) - CalendarioOperativo.Desfase, DateTimeKind.Utc);
            var hasta = desde.AddDays(1);
            var rutas = await db.Set<Ruta>().CountAsync(r => r.DespachadaEn >= desde && r.DespachadaEn < hasta && r.Rendicion == null, ct);
            var devoluciones = await db.Set<Devolucion>().CountAsync(d => d.FechaInicio >= desde && d.FechaInicio < hasta && d.Estado == EstadoDevolucion.EnProceso, ct);
            var fila = await db.Set<ResumenOperativoDiario>().SingleOrDefaultAsync(x => x.Dia == dia, ct);
            if (fila is null) { fila = new(dia); db.Add(fila); }
            var cerrado = fila.Recalcular(JsonSerializer.Serialize(new IndicadoresDia(reporte, rutas, devoluciones), Json), reloj.GetUtcNow().UtcDateTime, hoy);
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            actualizados++; if (cerrado) cerrados++;
        }
        return new(actualizados, cerrados, omitidos);
    }

    private AppDbContext Abrir(Guid operador) => new(opciones, TenantActual.ParaOperador(operador));
}
