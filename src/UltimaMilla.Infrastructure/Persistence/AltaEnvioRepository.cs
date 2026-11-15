using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Abstractions;
using UltimaMilla.Application.Envios.CrearEnvio;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Comercios;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Usuarios;

namespace UltimaMilla.Infrastructure.Persistence;

public sealed class AltaEnvioRepository(AppDbContext db) : IAltaEnvioRepository
{
    public async Task<ReferenciasAltaEnvio> CargarAsync(CrearEnvioCommand command, CancellationToken cancellationToken)
    {
        var tenant = db.ContextoTenant;
        if (tenant is not { Origen: OrigenTenant.UsuarioAutenticado, UsuarioId: not null })
            throw new AislamientoOperadorException();
        var cuenta = await db.Set<CuentaComercial>().Include(c => c.Operador).Include(c => c.Comercio)
            .SingleOrDefaultAsync(c => c.Id == command.CuentaComercialId, cancellationToken)
            ?? throw new AislamientoOperadorException();
        if (command.ComercioId != Guid.Empty && cuenta.ComercioId != command.ComercioId)
            throw new AislamientoOperadorException();
        if (!cuenta.Activa) throw new InvalidOperationException("La cuenta comercial está dada de baja.");
        var usuario = await db.Set<Usuario>().Include(u => u.Operador).Include(u => u.CuentasComerciales)
            .SingleOrDefaultAsync(u => u.Id == tenant.UsuarioId && u.Activo, cancellationToken)
            ?? throw new AislamientoOperadorException();
        if (usuario.Rol != tenant.Rol || usuario.Rol == RolUsuario.Comercio &&
            !usuario.CuentasComerciales.Any(c => c.Id == cuenta.Id))
            throw new AislamientoOperadorException();
        var zona = await db.Set<ZonaCobertura>().Include(z => z.Franjas)
            .SingleOrDefaultAsync(z => z.Id == command.ZonaId, cancellationToken)
            ?? throw new AislamientoOperadorException();
        var reglas = await db.Set<ReglasOperativas>().Include(r => r.PlazosServicio).Include(r => r.PoliticaDevolucion)
            .SingleOrDefaultAsync(r => r.Id == command.ReglasId, cancellationToken)
            ?? throw new AislamientoOperadorException();
        // El dominio valida que haya línea para la zona (LineaTarifa.ZonaId navega a la zona): hay que cargarla.
        var tarifario = await db.Set<CuadroTarifario>().Include(c => c.Lineas).ThenInclude(l => l.Zona)
            .SingleOrDefaultAsync(c => c.Id == command.CuadroTarifarioId, cancellationToken)
            ?? throw new AislamientoOperadorException();
        return new(cuenta, zona, reglas, tarifario, usuario);
    }

    public async Task GuardarAsync(Envio envio, CancellationToken cancellationToken)
    {
        db.Envios.Add(envio);
        await db.SaveChangesAsync(cancellationToken);
    }
}
