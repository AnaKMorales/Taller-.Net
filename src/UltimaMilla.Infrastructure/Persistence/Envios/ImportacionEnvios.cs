using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.AltaPortal;
using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Envios.Importacion;
using UltimaMilla.Application.Tarifas;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Infrastructure.Persistence.Envios;

public sealed class ImportacionEnvios(DbContextOptions<AppDbContext> options, ITenantActual tenantActual,
    ITarifarioVigente tarifarios, TimeProvider reloj, OpcionesOperacion operacion) : IImportacionEnvios
{
    public async Task<ResultadoFilaImportacion> ImportarFilaAsync(FilaImportacion fila, CancellationToken ct = default)
    {
        var tenant = tenantActual.Contexto;
        if (tenant is not { Origen: OrigenTenant.UsuarioAutenticado, Rol: RolUsuario.Comercio,
                CuentaComercialId: Guid cuentaId, UsuarioId: Guid usuarioId }) throw new AislamientoOperadorException();
        ArgumentException.ThrowIfNullOrWhiteSpace(fila.Referencia);
        if (fila.Referencia.Length > 100 || fila.Referencia != fila.Referencia.Trim())
            throw new ArgumentException("Referencia de importación inválida.");
        await using var db = new AppDbContext(options, new TenantActual(tenant));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serializa solo la misma referencia dentro de su cuenta. El índice único sigue siendo la garantía SQL.
        var lockBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{tenant.OperadorId:N}/{cuentaId:N}/{fila.Referencia}"));
        var lockId = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(lockBytes);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", ct);
        if (!await db.Set<Usuario>().AnyAsync(u => u.Id == usuarioId && u.Activo && u.Rol == RolUsuario.Comercio &&
                u.CuentasComerciales.Any(c => c.Id == cuentaId), ct)) throw new AislamientoOperadorException();
        var huella = Huella(fila);
        var anterior = await db.Set<AltaImportada>().SingleOrDefaultAsync(x => x.Referencia == fila.Referencia, ct);
        if (anterior is not null)
            return anterior.Huella == huella
                ? new(fila.Referencia, EstadoFilaImportacion.Existente, JsonSerializer.Deserialize<AltaCreada>(anterior.Resultado)!)
                : new(fila.Referencia, EstadoFilaImportacion.Conflicto, Error: "La referencia ya existe con otros datos.");

        var alta = new AltaEnvioPortalHandler(new OpcionesAlta(db, reloj), tarifarios, new CalculadorTarifa(),
            new CrearEnvioHandler(new AltaEnvioRepository(db), reloj), reloj, operacion);
        var creada = await alta.ConfirmarAsync(fila.Solicitud, fila.ClaveEntrega, ct);
        var envio = db.Envios.Local.Single(e => e.CodigoSeguimiento == creada.CodigoSeguimiento);
        db.Add(new AltaImportada(envio.CuentaComercial, envio, fila.Referencia, huella, JsonSerializer.Serialize(creada)));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(fila.Referencia, EstadoFilaImportacion.Creado, creada);
    }

    private static string Huella(FilaImportacion fila)
    {
        var s = fila.Solicitud;
        if (s.Bultos is null || s.Bultos.Any(b => b is null)) throw new ArgumentException("Faltan los datos de los bultos.");
        static string Numero(decimal valor) => valor.ToString("G29", CultureInfo.InvariantCulture);
        var contenido = JsonSerializer.Serialize(new { s.ZonaId, s.Modalidad, s.DestinatarioNombre,
            s.DestinatarioTelefono, s.DestinatarioEmail, s.Calle, s.Numero, s.Localidad,
            fila.ClaveEntrega, Bultos = s.Bultos.Select(b => new { b.Codigo, Peso = Numero(b.Peso),
                Alto = Numero(b.Alto), Ancho = Numero(b.Ancho), Largo = Numero(b.Largo) }).OrderBy(b => b.Codigo, StringComparer.Ordinal) });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)));
    }
}
