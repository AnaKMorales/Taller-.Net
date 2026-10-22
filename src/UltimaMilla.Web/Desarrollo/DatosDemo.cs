using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Comercios;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Operadores;
using UltimaMilla.Domain.Usuarios;
using UltimaMilla.Infrastructure.Persistence;

namespace UltimaMilla.Web.Desarrollo;

public sealed record DatosDemoCreados(Guid OperadorId, string CodigoSeguimiento);

// Crea un operador con su configuración, un tarifario y un envío, para probar la caché sin login ni alta.
// Cada llamada crea un operador nuevo, así se puede mostrar que la caché no mezcla operadores.
public static class DatosDemo
{
    public static async Task<DatosDemoCreados> CrearAsync(DbContextOptions<AppDbContext> options, CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        var operador = new Operador($"Operador demo {sufijo}");
        var admin = new Usuario(operador, $"admin-{sufijo}@demo.uy", RolUsuario.Administrador);
        var cuenta = new CuentaComercial(operador, new Comercio($"Comercio demo {sufijo}", "099000001", $"comercio-{sufijo}@demo.uy"));
        var zona = new ZonaCobertura(operador, "Centro", "11000", [new FranjaHoraria("Día", new(8, 0), new(18, 0))]);

        var reglas = new ReglasOperativas(operador, 1, ahora.AddDays(-1), 2, 0, [TipoPrueba.Foto],
            new PoliticaDevolucion(1, 5, true));
        _ = new PlazoServicio(reglas, ModalidadServicio.Estandar, 24, new(16, 0));
        _ = new MotivoNoEntrega(reglas, "Ausente", "Nadie atendió", Responsable.Destinatario, true, true);

        var cuadro = new CuadroTarifario(operador, 1, ahora.AddDays(-1));
        var linea = new LineaTarifa(cuadro, zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
        _ = new AjusteTarifario(linea, TipoAjuste.Recargo, 10);

        var destinatario = new Destinatario("María Pérez", "099123456", "maria@example.com",
            new Direccion("Av. 18 de Julio", "1234", "Montevideo", "11100"));
        var envio = new Envio(cuenta, destinatario, [new Bulto("B001", 2.5m, 10, 20, 30)], ModalidadServicio.Estandar,
            zona, reglas, cuadro, 165, ahora, ahora.AddHours(1),
            ProgramacionEntrega.Crear(zona.Franjas.Single(), DateOnly.FromDateTime(ahora.AddDays(1)), TimeSpan.FromHours(-3)),
            admin);

        await using var db = new AppDbContext(options, TenantActual.ParaOperador(operador.Id));
        db.Add(envio);
        await db.SaveChangesAsync(ct);
        return new DatosDemoCreados(operador.Id, envio.CodigoSeguimiento);
    }
}
