using Microsoft.EntityFrameworkCore;
using UltimaMilla.Application.Tenancy;
using UltimaMilla.Domain.Common;
using UltimaMilla.Domain.Comercios;
using UltimaMilla.Domain.Configuracion;
using UltimaMilla.Domain.Deposito;
using UltimaMilla.Domain.Despacho;
using UltimaMilla.Domain.Envios;
using UltimaMilla.Domain.Notificaciones;
using UltimaMilla.Domain.Operadores;
using UltimaMilla.Domain.Usuarios;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Web.Desarrollo;

public sealed record DatosDemoCreados(Guid OperadorId, string EmailAdministrador, string EmailComercio,
    string EmailRepartidor, string Contrasena, string CodigoSeguimiento);

// Datos de prueba, solo para desarrollo. Cada llamada crea un operador nuevo.
// (/dev/datos-demo usa EscenarioDemo, con dos operadores de configuración distinta; este se usa en /dev/datos-demo-pwa.)
public static class DatosDemo
{
    private static readonly GeoPunto Montevideo = new(-34.9011m, -56.1645m);

    private const string ContrasenaDemo = "Demo1234!";

    public static async Task<DatosDemoCreados> CrearAsync(DbContextOptions<AppDbContext> options, CancellationToken ct,
        bool incluirVisitaParcial = false)
    {
        var ahora = incluirVisitaParcial ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow;
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        var operador = new Operador($"Operador demo {sufijo}");
        var admin = new Usuario(operador, $"admin-{sufijo}@demo.uy", RolUsuario.Administrador);
        var repartidor = new Repartidor(operador, $"repartidor-{sufijo}@demo.uy");
        var cuenta = new CuentaComercial(operador, new Comercio($"Comercio demo {sufijo}", "099000001", $"comercio-{sufijo}@demo.uy"));
        var usuarioComercio = new Usuario(operador, $"comercio-{sufijo}@demo.uy", RolUsuario.Comercio);
        usuarioComercio.AsignarCuentaComercial(cuenta);
        var zona = new ZonaCobertura(operador, "Centro", "11000", [new FranjaHoraria("Día", new(8, 0), new(18, 0))]);

        var reglas = new ReglasOperativas(operador, 1, ahora.AddDays(-1), 2, 0, [TipoPrueba.Foto],
            new PoliticaDevolucion(1, 5, true), permiteEntregaParcial: incluirVisitaParcial);
        _ = new PlazoServicio(reglas, ModalidadServicio.Estandar, 24, new(16, 0));
        var ausente = new MotivoNoEntrega(reglas, "Ausente", "Nadie atendió", Responsable.Destinatario, true, true);

        var cuadro = new CuadroTarifario(operador, 1, ahora.AddDays(-1));
        var linea = new LineaTarifa(cuadro, zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
        _ = new AjusteTarifario(linea, TipoAjuste.Recargo, 10);
        var vehiculo = new Vehiculo(operador, $"DEMO{sufijo[..4].ToUpperInvariant()}", 100, 10);

        var admitido = NuevoEnvio(cuenta, zona, reglas, cuadro, admin, "María Pérez", ahora);

        var fallido = NuevoEnvio(cuenta, zona, reglas, cuadro, admin, "Juan Rodríguez", ahora);
        var bulto = fallido.Bultos.Single();
        EscaneoBulto.RegistrarRecepcion(bulto, admin, ahora.AddSeconds(1));
        fallido.RecibirEnDeposito(admin, ahora.AddSeconds(1));
        _ = new Discrepancia(fallido, bulto, TipoDiscrepancia.PesoNoCoincide, "Peso del bulto", "2.5 kg", "3.1 kg",
            ahora.AddSeconds(1));

        // Entregado antes de la fecha comprometida: cuenta como cumplido en los reportes.
        var entregado = NuevoEnvio(cuenta, zona, reglas, cuadro, admin, "Ana Gómez", ahora);
        EscaneoBulto.RegistrarRecepcion(entregado.Bultos.Single(), admin, ahora.AddSeconds(1));
        entregado.RecibirEnDeposito(admin, ahora.AddSeconds(1));

        Envio? visitaParcial = null;
        if (incluirVisitaParcial)
        {
            visitaParcial = NuevoEnvio(cuenta, zona, reglas, cuadro, admin, "Entrega parcial de prueba", ahora,
                [new Bulto("PARCIAL-01", 2.5m, 10, 20, 30), new Bulto("PARCIAL-02", 1m, 10, 20, 30)]);
            foreach (var pendiente in visitaParcial.Bultos)
                EscaneoBulto.RegistrarRecepcion(pendiente, admin, ahora.AddSeconds(1));
            visitaParcial.RecibirEnDeposito(admin, ahora.AddSeconds(1));
        }

        // Los envíos deben existir antes de insertar las paradas que los referencian.
        await using var db = new AppDbContext(options, TenantActual.ParaOperador(operador.Id));
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        db.AddRange(admitido, fallido, entregado, admin, usuarioComercio, repartidor);
        if (visitaParcial is not null) db.Add(visitaParcial);
        foreach (var usuario in new[] { admin, usuarioComercio, repartidor })
            ContrasenasUsuario.Asignar(db, usuario, ContrasenaDemo);
        await db.SaveChangesAsync(ct);

        var ruta = new Ruta(repartidor, vehiculo, 10);
        ruta.AgregarParada(fallido, 1, fallido.VentanaDesde.AddHours(1), admin, ahora.AddSeconds(2));
        ruta.AgregarParada(entregado, 2, entregado.VentanaDesde.AddHours(2), admin, ahora.AddSeconds(2));
        if (visitaParcial is not null)
            ruta.AgregarParada(visitaParcial, 3, visitaParcial.VentanaDesde.AddHours(3), admin, ahora.AddSeconds(2));
        db.Add(ruta);
        await db.SaveChangesAsync(ct);

        ruta.Despachar(admin, ahora.AddSeconds(3));
        fallido.RegistrarIntento(ResultadoIntento.Fallido, ausente,
            [new PruebaEntrega(TipoPrueba.Foto, ahora.AddSeconds(4), Montevideo, new ArchivoRef("evidencias/demo.jpg"))],
            repartidor, ahora.AddSeconds(4), Guid.NewGuid(), Montevideo);
        entregado.RegistrarIntento(ResultadoIntento.Entregado, null,
            [new PruebaEntrega(TipoPrueba.Foto, ahora.AddSeconds(4), Montevideo, new ArchivoRef("evidencias/demo-ok.jpg"))],
            repartidor, ahora.AddSeconds(4), Guid.NewGuid(), Montevideo);

        var suscripcion = new SuscripcionAviso(cuenta, [TipoEvento.FalloEntrega, TipoEvento.EntregaExitosa], "secreto-demo",
            new Uri("https://comercio-demo.example/webhooks/envios"));
        var aviso = new EntregaAviso(suscripcion, fallido.Eventos.Last());
        aviso.RegistrarIntento(false, ahora.AddSeconds(5));
        aviso.RegistrarIntento(false, ahora.AddSeconds(6));
        new EntregaAviso(suscripcion, entregado.Eventos.Last()).RegistrarIntento(true, ahora.AddSeconds(5));

        db.Add(suscripcion);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return new DatosDemoCreados(operador.Id, admin.Email, usuarioComercio.Email, repartidor.Email, ContrasenaDemo,
            (visitaParcial ?? admitido).CodigoSeguimiento);
    }

    public static async Task SimularAltaAsync(DbContextOptions<AppDbContext> options, Guid operadorId, CancellationToken ct)
    {
        await using var db = new AppDbContext(options, TenantActual.ParaOperador(operadorId));
        _ = await db.Set<Operador>().SingleAsync(o => o.Id == operadorId, ct);
        var admin = await Administrador(db, ct);
        var cuenta = await db.Set<CuentaComercial>().Include(c => c.Comercio).FirstAsync(ct);
        var zona = await db.Set<ZonaCobertura>().Include(z => z.Franjas).FirstAsync(ct);
        var reglas = await db.Set<ReglasOperativas>().Include(r => r.PlazosServicio).Include(r => r.PoliticaDevolucion)
            .OrderByDescending(r => r.Version).FirstAsync(ct);
        var cuadro = await db.Set<CuadroTarifario>().Include(c => c.Lineas)
            .OrderByDescending(c => c.Version).FirstAsync(ct);

        db.Add(NuevoEnvio(cuenta, zona, reglas, cuadro, admin, "Destinatario simulado", DateTime.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    // Suscribe la primera cuenta del operador a los tipos de evento indicados, con tres destinos del receptor de prueba
    // (tools/UltimaMilla.ReceptorWebhooks): /ok, /flaky?n=2 y /fail. Es idempotente por URL.
    public static async Task<IReadOnlyList<string>> SuscribirReceptorAsync(DbContextOptions<AppDbContext> options,
        Guid operadorId, Uri receptor, string secreto, IReadOnlyCollection<TipoEvento> tipos, CancellationToken ct)
    {
        await using var db = new AppDbContext(options, TenantActual.ParaOperador(operadorId));
        _ = await db.Set<Operador>().SingleAsync(o => o.Id == operadorId, ct);
        var cuenta = await db.Set<CuentaComercial>().FirstAsync(ct);
        var existentes = (await db.Set<SuscripcionAviso>().ToListAsync(ct)).Select(s => s.UrlDestino.AbsoluteUri).ToHashSet();
        var base_ = receptor.AbsoluteUri.TrimEnd('/');
        var destinos = new[] { "/ok", "/flaky?n=2", "/fail" }.Select(ruta => new Uri(base_ + ruta)).ToList();
        foreach (var destino in destinos.Where(d => !existentes.Contains(d.AbsoluteUri)))
            db.Add(new SuscripcionAviso(cuenta, tipos, secreto, destino));
        await db.SaveChangesAsync(ct);
        return destinos.Select(d => d.AbsoluteUri).ToList();
    }

    public static async Task<bool> SimularRecepcionAsync(DbContextOptions<AppDbContext> options, Guid operadorId, CancellationToken ct)
    {
        await using var db = new AppDbContext(options, TenantActual.ParaOperador(operadorId));
        _ = await db.Set<Operador>().SingleAsync(o => o.Id == operadorId, ct);
        var envio = await db.Envios
            .Include(e => e.CuentaComercial).Include(e => e.Zona).Include(e => e.Reglas)
            .Include(e => e.CuadroTarifario).Include(e => e.Destinatario).Include(e => e.Programacion)
            .Include(e => e.Bultos).ThenInclude(b => b.Escaneos)
            .Where(e => e.Estado == EstadoEnvio.Admitido)
            .OrderBy(e => e.FechaAdmision)
            .FirstOrDefaultAsync(ct);
        if (envio is null) return false;

        var admin = await Administrador(db, ct);
        var ahora = DateTime.UtcNow;
        foreach (var bulto in envio.Bultos) EscaneoBulto.RegistrarRecepcion(bulto, admin, ahora);
        envio.RecibirEnDeposito(admin, ahora);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static Task<Usuario> Administrador(AppDbContext db, CancellationToken ct) =>
        db.Set<Usuario>().FirstAsync(u => u.Rol == RolUsuario.Administrador && u.Activo, ct);

    internal static Envio NuevoEnvio(CuentaComercial cuenta, ZonaCobertura zona, ReglasOperativas reglas,
        CuadroTarifario cuadro, Usuario responsable, string destinatario, DateTime ahora, IReadOnlyList<Bulto>? bultos = null,
        FranjaHoraria? franja = null, ModalidadServicio modalidad = ModalidadServicio.Estandar, decimal tarifa = 165,
        Direccion? direccion = null) =>
        new(cuenta, new Destinatario(destinatario, "099123456", "destinatario@example.com",
                direccion ?? new Direccion("Av. 18 de Julio", "1234", "Montevideo", "11100")),
            bultos ?? [new Bulto("B001", 2.5m, 10, 20, 30)], modalidad, zona, reglas, cuadro, tarifa,
            ahora, ahora.AddHours(1),
            ProgramacionEntrega.Crear(franja ?? zona.Franjas.Single(), DateOnly.FromDateTime(ahora.AddDays(1)), TimeSpan.FromHours(-3)),
            responsable);
}
