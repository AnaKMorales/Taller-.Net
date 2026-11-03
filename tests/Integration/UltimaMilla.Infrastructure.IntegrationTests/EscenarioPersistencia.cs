namespace UltimaMilla.Infrastructure.IntegrationTests;

internal sealed class EscenarioPersistencia
{
    internal static readonly DateTime Fecha = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);
    internal Operador Operador { get; } = new("Operador de integración");
    internal Usuario Admin { get; }
    internal Repartidor Repartidor { get; }
    internal ZonaCobertura Zona { get; }
    internal ReglasOperativas Reglas { get; }
    internal Vehiculo Vehiculo { get; }
    internal Envio Envio { get; }

    internal EscenarioPersistencia(bool reintentable = false, Comercio? comercio = null, bool parcial = false,
        AccionBultosPendientes accion = AccionBultosPendientes.NuevaVisitaSinRecargo, decimal? recargo = null)
    {
        Admin = new Usuario(Operador, "admin@example.com", RolUsuario.Administrador);
        Repartidor = new Repartidor(Operador, "repartidor@example.com");
        var cuenta = new CuentaComercial(Operador, comercio ?? new Comercio("Comercio", "099000001", "comercio@example.com"));
        var usuarioComercio = new Usuario(Operador, "usuario@example.com", RolUsuario.Comercio);
        usuarioComercio.AsignarCuentaComercial(cuenta);
        Zona = new ZonaCobertura(Operador, "Centro", "11000", [new FranjaHoraria("Día", new(8, 0), new(18, 0))]);
        Reglas = new ReglasOperativas(Operador, 1, Fecha.AddDays(-1), 2, 0,
            [TipoPrueba.Foto], new PoliticaDevolucion(1, 5, true), parcial, accion, recargo);
        _ = new PlazoServicio(Reglas, ModalidadServicio.Estandar, 24, new(16, 0));
        _ = new MotivoNoEntrega(Reglas, "Ausente o rechazo", "Motivo de prueba", Responsable.Destinatario, true, reintentable);
        var cuadro = new CuadroTarifario(Operador, 1, Fecha.AddDays(-1));
        var linea = new LineaTarifa(cuadro, Zona, ModalidadServicio.Estandar, 0, 100, 10, 150);
        _ = new AjusteTarifario(linea, TipoAjuste.Recargo, 10);
        Vehiculo = new Vehiculo(Operador, "ABC123", 100, 10);
        Envio = new Envio(cuenta, new Destinatario("Persona", "099000002", "persona@example.com",
                new Direccion("Calle", "0010 bis", "Montevideo", "01100")),
            parcial ? [new Bulto("B001", 2.1234567m, 10, 20, 30), new Bulto("B002", 1, 10, 10, 10), new Bulto("B003", 1, 10, 10, 10)] :
            [new Bulto("B001", 2.1234567m, 10, 20, 30)], ModalidadServicio.Estandar, Zona, Reglas, cuadro,
            150.25m, Fecha, Fecha.AddHours(1),
            ProgramacionEntrega.Crear(Zona.Franjas.Single(), new(2026, 10, 14), TimeSpan.FromHours(-3)), Admin);
    }

    internal void Recibir()
    {
        foreach (var bulto in Envio.Bultos) EscaneoBulto.RegistrarRecepcion(bulto, Admin, Fecha);
        Envio.RecibirEnDeposito(Admin, Fecha);
    }

    internal Ruta Asignar(DateTime? fecha = null, Vehiculo? vehiculo = null)
    {
        var momento = fecha ?? Fecha;
        var ruta = new Ruta(Repartidor, vehiculo ?? Vehiculo, 10);
        ruta.AgregarParada(Envio, 1, momento.AddHours(1), Admin, momento);
        return ruta;
    }

    internal PruebaEntrega Foto(DateTime? fecha = null) => new(TipoPrueba.Foto, fecha ?? Fecha.AddHours(1),
        new GeoPunto(-34.9011m, -56.1645m), new ArchivoRef("evidencias/foto.jpg"));

    internal static IQueryable<Envio> Grafo(AppDbContext db, bool incluirDevolucion = true)
    {
        var query = db.Envios.AsSplitQuery()
        .Include(e => e.CuentaComercial).ThenInclude(c => c.Operador)
        .Include(e => e.CuentaComercial).ThenInclude(c => c.Comercio)
        .Include(e => e.CuentaComercial).ThenInclude(c => c.Usuarios)
        .Include(e => e.Destinatario).ThenInclude(d => d.Direccion)
        .Include(e => e.Destinatario).ThenInclude(d => d.Notificaciones)
        .Include(e => e.Zona).ThenInclude(z => z.Franjas)
        .Include(e => e.Reglas).ThenInclude(r => r.PoliticaDevolucion)
        .Include(e => e.Reglas).ThenInclude(r => r.PlazosServicio)
        .Include(e => e.Reglas).ThenInclude(r => r.MotivosNoEntrega)
        .Include(e => e.CuadroTarifario).ThenInclude(c => c.Lineas).ThenInclude(l => l.Ajustes)
        .Include(e => e.Programacion).ThenInclude(p => p.Franja)
        .Include(e => e.Bultos).ThenInclude(b => b.Escaneos)
        .Include(e => e.Eventos).ThenInclude(v => v.Responsable)
        .Include(e => e.Eventos).ThenInclude(v => v.ProgramacionAnterior).ThenInclude(p => p!.Franja)
        .Include(e => e.Eventos).ThenInclude(v => v.ProgramacionNueva).ThenInclude(p => p!.Franja)
        .Include(e => e.Intentos).ThenInclude(i => i.Pruebas)
        .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Bulto)
        .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Motivo)
        .Include(e => e.Intentos).ThenInclude(i => i.Detalles).ThenInclude(d => d.Evidencias).ThenInclude(p => p.Prueba)
        .Include(e => e.Intentos).ThenInclude(i => i.MotivoNoEntrega)
        .Include(e => e.Intentos).ThenInclude(i => i.Parada).ThenInclude(p => p.Ruta).ThenInclude(r => r.Vehiculo)
        .Include(e => e.Parada).ThenInclude(p => p!.Ruta)
        .Include(e => e.Parada).ThenInclude(p => p!.Bultos).ThenInclude(b => b.Bulto)
        .Include(e => e.Parada).ThenInclude(p => p!.Recargo)
        .Include(e => e.Recargos).ThenInclude(r => r.Parada)
        .Include(e => e.Intentos).ThenInclude(i => i.Parada).ThenInclude(p => p.Bultos).ThenInclude(b => b.Bulto)
        .Include(e => e.Discrepancias);
        return incluirDevolucion ? query.Include(e => e.Devolucion).ThenInclude(d => d!.Bultos).ThenInclude(b => b.Bulto) : query;
    }
}
