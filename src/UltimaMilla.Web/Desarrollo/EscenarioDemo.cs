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
using UltimaMilla.Infrastructure.Persistence.Identidad;
using UltimaMilla.Infrastructure.Persistence.Tenancy;

namespace UltimaMilla.Web.Desarrollo;

/// <summary>
/// Cuentas y resumen de configuración de un operador demo. <paramref name="Contrasena"/> solo viene cuando se usó la clave
/// por defecto (desarrollo local); si la indicó quien llamó, no se devuelve (ya la conoce).
/// </summary>
public sealed record CuentaDemo(Guid OperadorId, string Operador, string EmailAdministrador, string EmailComercio,
    string EmailRepartidor, string? Contrasena, string SeguimientoPendiente, string SeguimientoParcial,
    string SeguimientoFallido, IReadOnlyList<string> Configuracion);

/// <summary>Cuerpo opcional de POST /dev/datos-demo: clave de las cuentas (mínimo 12 caracteres).</summary>
public sealed record SolicitudDatosDemo(string? Contrasena)
{
    public const int LargoMinimo = 12;
}

public sealed record EscenarioDemoCreado(IReadOnlyList<CuentaDemo> Operadores);

/// <summary>
/// Escenario de demostración (solo desarrollo): dos operadores con configuraciones distintas (zonas, franjas, tarifas,
/// reglas de entrega y motivos de no entrega), para mostrar el aislamiento y que cada uno tiene su propio tratamiento.
/// Cada operador deja un repartidor con una hoja de ruta despachada que tiene una visita pendiente, una visita con
/// entrega parcial por bulto, un envío no entregado con un aviso fallido y un envío entregado.
/// </summary>
public static class EscenarioDemo
{
    /// <summary>Clave por defecto, solo para desarrollo local. En un servidor público se pasa una clave propia.</summary>
    public const string ContrasenaDemo = "Demo1234!";

    private sealed record FranjaDef(string Nombre, int Desde, int Hasta);

    private sealed record ZonaDef(string Nombre, string CodigoPostal, FranjaDef[] Franjas, decimal Estandar,
        decimal? Urgente, Func<Direccion> NuevaDireccion);

    private sealed record MotivoDef(string Nombre, string Descripcion, Responsable Responsable, bool Evidencia,
        bool Reintentable);

    private sealed record PlazoDef(int Horas, TimeOnly Corte);

    // Dónde cae cada envío del escenario: zona, franja y modalidad.
    private sealed record UbicacionDef(int Zona, int Franja, bool Urgente);

    private sealed record PerfilDef(
        string Clave, string Nombre, GeoPunto Punto, ZonaDef[] Zonas, decimal RecargoEstandarPct,
        int MaxIntentos, int HorasEntreIntentos, TipoPrueba[] Pruebas,
        int DiasEnDeposito, int PlazoDevolucionDias, bool DevolverAlAgotar,
        AccionBultosPendientes Accion, decimal? ImporteRecargo,
        PlazoDef Estandar, PlazoDef? Urgente, MotivoDef[] Motivos,
        UbicacionDef Pendiente, UbicacionDef Parcial, UbicacionDef Fallido, UbicacionDef Entregado, UbicacionDef Admitido,
        int BultosParcial, int EntregadosParcial, int MotivoParcial, int MotivoFallido, int IntentosAvisoFallido,
        string[] Destinatarios);

    // Operador A: ciudad, dos zonas con franjas partidas, servicio urgente, tres intentos y firma + foto.
    private static readonly PerfilDef Express = new(
        "express", "Express Montevideo", new GeoPunto(-34.9011m, -56.1645m),
        [
            new ZonaDef("Centro", "11000", [new("Mañana", 8, 12), new("Tarde", 14, 19)], 140m, 260m,
                () => new Direccion("Av. 18 de Julio", "1234", "Montevideo", "11100")),
            new ZonaDef("Pocitos", "11300", [new("Todo el día", 9, 20)], 180m, 300m,
                () => new Direccion("Bvar. España", "2100", "Montevideo", "11300")),
        ],
        RecargoEstandarPct: 10, MaxIntentos: 3, HorasEntreIntentos: 2, Pruebas: [TipoPrueba.Foto, TipoPrueba.Firma],
        DiasEnDeposito: 2, PlazoDevolucionDias: 7, DevolverAlAgotar: true,
        Accion: AccionBultosPendientes.NuevaVisitaSinRecargo, ImporteRecargo: null,
        Estandar: new PlazoDef(24, new TimeOnly(16, 0)), Urgente: new PlazoDef(4, new TimeOnly(12, 0)),
        Motivos:
        [
            new MotivoDef("Ausente", "Nadie atendió en el domicilio", Responsable.Destinatario, true, true),
            new MotivoDef("Dirección incorrecta", "La dirección no existe o está incompleta", Responsable.Comercio, true, false),
            new MotivoDef("Rechazó el paquete", "El destinatario no quiso recibirlo", Responsable.Destinatario, true, false),
        ],
        Pendiente: new UbicacionDef(0, 0, true), Parcial: new UbicacionDef(1, 0, false),
        Fallido: new UbicacionDef(0, 1, false), Entregado: new UbicacionDef(1, 0, true), Admitido: new UbicacionDef(0, 0, false),
        BultosParcial: 3, EntregadosParcial: 2, MotivoParcial: 2, MotivoFallido: 0, IntentosAvisoFallido: 2,
        Destinatarios: ["Lucía Fernández", "Martín Sosa", "Valentina Pereira", "Diego Acosta", "Camila Techera"]);

    // Operador B: interior, tarifas más altas por distancia, sin urgente, dos intentos, foto + nombre de quien recibe,
    // y nueva visita con recargo fijo para los bultos pendientes.
    private static readonly PerfilDef Interior = new(
        "interior", "Logística del Interior", new GeoPunto(-34.5228m, -56.2778m),
        [
            new ZonaDef("Canelones", "90000", [new("Jornada", 9, 17)], 320m, null,
                () => new Direccion("Ruta 5", "km 30", "Canelones", "90000")),
            new ZonaDef("Costa de Oro", "15000", [new("Mañana", 9, 13)], 410m, null,
                () => new Direccion("Av. Giannattasio", "km 28", "Ciudad de la Costa", "15000")),
        ],
        RecargoEstandarPct: 15, MaxIntentos: 2, HorasEntreIntentos: 0, Pruebas: [TipoPrueba.Foto, TipoPrueba.Nombre],
        DiasEnDeposito: 5, PlazoDevolucionDias: 14, DevolverAlAgotar: false,
        Accion: AccionBultosPendientes.NuevaVisitaConRecargo, ImporteRecargo: 120m,
        Estandar: new PlazoDef(72, new TimeOnly(14, 0)), Urgente: null,
        Motivos:
        [
            new MotivoDef("Zona de difícil acceso", "Camino anegado o sin acceso para el vehículo", Responsable.FuerzaMayor, true, true),
            new MotivoDef("Destinatario ausente", "No había nadie para recibir", Responsable.Destinatario, true, true),
            new MotivoDef("Pago contra entrega no disponible", "El destinatario no tenía el importe", Responsable.Comercio, false, false),
        ],
        Pendiente: new UbicacionDef(0, 0, false), Parcial: new UbicacionDef(1, 0, false),
        Fallido: new UbicacionDef(0, 0, false), Entregado: new UbicacionDef(1, 0, false), Admitido: new UbicacionDef(0, 0, false),
        BultosParcial: 2, EntregadosParcial: 1, MotivoParcial: 0, MotivoFallido: 1, IntentosAvisoFallido: 3,
        Destinatarios: ["Marta Rodríguez", "Gonzalo Techera", "Sofía Cabrera", "Nicolás Duarte", "Elena Píriz"]);

    public static async Task<EscenarioDemoCreado> CrearAsync(DbContextOptions<AppDbContext> options, string? contrasena,
        CancellationToken ct)
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var express = await CrearOperadorAsync(options, Express, sufijo, contrasena, ct);
        var interior = await CrearOperadorAsync(options, Interior, sufijo, contrasena, ct);
        return new EscenarioDemoCreado([express, interior]);
    }

    private static async Task<CuentaDemo> CrearOperadorAsync(DbContextOptions<AppDbContext> options, PerfilDef p,
        string sufijo, string? contrasenaIndicada, CancellationToken ct)
    {
        var contrasena = contrasenaIndicada ?? ContrasenaDemo;
        var ahora = DateTime.UtcNow;
        var operador = new Operador($"{p.Nombre} [{sufijo}]");
        var admin = new Usuario(operador, $"admin-{p.Clave}-{sufijo}@demo.uy", RolUsuario.Administrador);
        var repartidor = new Repartidor(operador, $"repartidor-{p.Clave}-{sufijo}@demo.uy");
        var cuenta = new CuentaComercial(operador,
            new Comercio($"Comercio de {p.Nombre} [{sufijo}]", "099000001", $"comercio-{p.Clave}-{sufijo}@demo.uy"));
        var usuarioComercio = new Usuario(operador, $"comercio-{p.Clave}-{sufijo}@demo.uy", RolUsuario.Comercio);
        usuarioComercio.AsignarCuentaComercial(cuenta);

        // Zonas y franjas propias de cada operador.
        var zonas = p.Zonas.Select(z => new ZonaCobertura(operador, z.Nombre, z.CodigoPostal,
            z.Franjas.Select(f => new FranjaHoraria(f.Nombre, new TimeOnly(f.Desde, 0), new TimeOnly(f.Hasta, 0))))).ToList();

        // Reglas de entrega: intentos, pruebas, entrega parcial, qué hacer con los pendientes, plazos y motivos.
        var reglas = new ReglasOperativas(operador, 1, ahora.AddDays(-1), p.MaxIntentos, p.HorasEntreIntentos, p.Pruebas,
            new PoliticaDevolucion(p.DiasEnDeposito, p.PlazoDevolucionDias, p.DevolverAlAgotar),
            permiteEntregaParcial: true, accionPendientes: p.Accion, importeRecargoNuevaVisita: p.ImporteRecargo);
        _ = new PlazoServicio(reglas, ModalidadServicio.Estandar, p.Estandar.Horas, p.Estandar.Corte);
        if (p.Urgente is { } urgente)
            _ = new PlazoServicio(reglas, ModalidadServicio.Urgente, urgente.Horas, urgente.Corte);
        var motivos = p.Motivos.Select(m =>
            new MotivoNoEntrega(reglas, m.Nombre, m.Descripcion, m.Responsable, m.Evidencia, m.Reintentable)).ToList();

        // Tarifario propio: precio por zona y modalidad, y un recargo porcentual distinto.
        var cuadro = new CuadroTarifario(operador, 1, ahora.AddDays(-1));
        for (var i = 0; i < zonas.Count; i++)
        {
            var estandar = new LineaTarifa(cuadro, zonas[i], ModalidadServicio.Estandar, 0, 50, 10, p.Zonas[i].Estandar);
            _ = new AjusteTarifario(estandar, TipoAjuste.Recargo, p.RecargoEstandarPct);
            if (p.Zonas[i].Urgente is { } precioUrgente)
                _ = new LineaTarifa(cuadro, zonas[i], ModalidadServicio.Urgente, 0, 50, 10, precioUrgente);
        }

        var vehiculo = new Vehiculo(operador, $"{p.Clave[..3].ToUpperInvariant()}{sufijo[..4].ToUpperInvariant()}", 500, 10);

        Envio Nuevo(string destinatario, UbicacionDef donde, params Bulto[] bultos)
        {
            var modalidad = donde.Urgente ? ModalidadServicio.Urgente : ModalidadServicio.Estandar;
            var precio = donde.Urgente ? p.Zonas[donde.Zona].Urgente!.Value : p.Zonas[donde.Zona].Estandar;
            return DatosDemo.NuevoEnvio(cuenta, zonas[donde.Zona], reglas, cuadro, admin, destinatario, ahora, bultos,
                zonas[donde.Zona].Franjas.ElementAt(donde.Franja), modalidad, precio, p.Zonas[donde.Zona].NuevaDireccion());
        }

        var codigo = p.Clave[..3].ToUpperInvariant();
        var pendiente = Nuevo(p.Destinatarios[0], p.Pendiente, new Bulto($"{codigo}-PEN-01", 2.5m, 10, 20, 30));
        var parcial = Nuevo(p.Destinatarios[1], p.Parcial, Enumerable.Range(1, p.BultosParcial)
            .Select(n => new Bulto($"{codigo}-PAR-{n:00}", 1.5m, 10, 20, 30)).ToArray());
        var fallido = Nuevo(p.Destinatarios[2], p.Fallido, new Bulto($"{codigo}-FAL-01", 3m, 10, 20, 30));
        var entregado = Nuevo(p.Destinatarios[3], p.Entregado, new Bulto($"{codigo}-ENT-01", 1m, 10, 20, 30));
        var admitido = Nuevo(p.Destinatarios[4], p.Admitido, new Bulto($"{codigo}-ADM-01", 2m, 10, 20, 30));

        // Los que salen a ruta se reciben primero en el depósito.
        foreach (var envio in new[] { pendiente, parcial, fallido, entregado })
        {
            foreach (var bulto in envio.Bultos) EscaneoBulto.RegistrarRecepcion(bulto, admin, ahora.AddSeconds(1));
            envio.RecibirEnDeposito(admin, ahora.AddSeconds(1));
        }

        await using var db = new AppDbContext(options, TenantActual.ParaOperador(operador.Id));
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        db.AddRange(pendiente, parcial, fallido, entregado, admitido, admin, usuarioComercio, repartidor);
        foreach (var usuario in new[] { admin, usuarioComercio, repartidor })
        {
            // Clave propia: se valida como antes (largo mínimo). Sin clave: Demo1234!, con la política de Identity.
            if (contrasenaIndicada is null) await FabricaCuentasAcceso.Predeterminada.CrearAsync(db, usuario, contrasena);
            else await FabricaCuentasAcceso.Predeterminada.CrearConClaveDemoAsync(db, usuario, contrasena, SolicitudDatosDemo.LargoMinimo);
        }
        await db.SaveChangesAsync(ct);

        // Hoja de ruta del repartidor: pendiente, parcial, fallido y entregado.
        var ruta = new Ruta(repartidor, vehiculo, 10);
        var orden = 1;
        foreach (var envio in new[] { pendiente, parcial, fallido, entregado })
        {
            ruta.AgregarParada(envio, orden, envio.VentanaDesde.AddMinutes(20 * orden), admin, ahora.AddSeconds(2));
            orden++;
        }
        db.Add(ruta);
        await db.SaveChangesAsync(ct);
        ruta.Despachar(admin, ahora.AddSeconds(3));

        // La visita pendiente queda en tránsito, sin intento. Las demás ya tienen su resultado.
        var hora = ahora.AddSeconds(4);
        fallido.RegistrarIntento(ResultadoIntento.Fallido, motivos[p.MotivoFallido],
            [Prueba(TipoPrueba.Foto, hora, p.Punto, "fallido")], repartidor, hora, Guid.NewGuid(), p.Punto);
        entregado.RegistrarIntento(ResultadoIntento.Entregado, null, p.Pruebas.Select(t => Prueba(t, hora, p.Punto, "entregado")),
            repartidor, hora, Guid.NewGuid(), p.Punto);

        // Entrega parcial por bulto: unos bultos se entregan con todas las pruebas y el resto no, con motivo y evidencia.
        var resultados = parcial.Bultos.Select((bulto, i) => i < p.EntregadosParcial
            ? new ResultadoBultoEntrega(bulto.Id, ResultadoIntento.Entregado, null,
                p.Pruebas.Select(t => Prueba(t, hora, p.Punto, bulto.Codigo.ToLowerInvariant())).ToArray())
            : new ResultadoBultoEntrega(bulto.Id, ResultadoIntento.Fallido, motivos[p.MotivoParcial],
                [Prueba(TipoPrueba.Foto, hora, p.Punto, bulto.Codigo.ToLowerInvariant())])).ToArray();
        parcial.RegistrarIntentoPorBulto(parcial.Parada!.Id, resultados, repartidor, hora, Guid.NewGuid(), p.Punto);

        // Avisos al comercio: el del envío no entregado queda fallido (varios intentos sin éxito), los demás se entregaron.
        var suscripcion = new SuscripcionAviso(cuenta,
            [TipoEvento.FalloEntrega, TipoEvento.EntregaExitosa, TipoEvento.EntregaParcial], "secreto-demo",
            new Uri($"https://{p.Clave}.comercio-demo.example/webhooks/envios"));
        var avisoFallido = new EntregaAviso(suscripcion, fallido.Eventos.Last());
        for (var i = 1; i <= p.IntentosAvisoFallido; i++) avisoFallido.RegistrarIntento(false, hora.AddSeconds(i));
        new EntregaAviso(suscripcion, parcial.Eventos.Last()).RegistrarIntento(true, hora.AddSeconds(1));
        new EntregaAviso(suscripcion, entregado.Eventos.Last()).RegistrarIntento(true, hora.AddSeconds(1));

        db.Add(suscripcion);
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return new CuentaDemo(operador.Id, operador.RazonSocial, admin.Email, usuarioComercio.Email, repartidor.Email,
            contrasenaIndicada is null ? ContrasenaDemo : null, pendiente.CodigoSeguimiento, parcial.CodigoSeguimiento, fallido.CodigoSeguimiento,
            Resumen(p, motivos));
    }

    private static PruebaEntrega Prueba(TipoPrueba tipo, DateTime hora, GeoPunto punto, string etiqueta) => tipo switch
    {
        TipoPrueba.Foto => new PruebaEntrega(tipo, hora, punto, new ArchivoRef($"evidencias/{etiqueta}.jpg")),
        TipoPrueba.Firma => new PruebaEntrega(tipo, hora, punto, new ArchivoRef($"evidencias/{etiqueta}-firma.png")),
        TipoPrueba.Nombre => new PruebaEntrega(tipo, hora, punto, valorTexto: "Recibió una persona adulta del domicilio"),
        _ => new PruebaEntrega(tipo, hora, punto, valorTexto: "Documento 1.234.567-8"),
    };

    // Texto para mostrar qué tiene de distinto cada operador (lo guarda sembrar-demo.sh junto a las cuentas).
    private static IReadOnlyList<string> Resumen(PerfilDef p, IReadOnlyList<MotivoNoEntrega> motivos)
    {
        static string Hora(int h) => $"{h:00}:00";
        string Zona(ZonaDef z) => $"{z.Nombre} (CP {z.CodigoPostal}: {string.Join(", ", z.Franjas.Select(f => $"{f.Nombre} {Hora(f.Desde)}-{Hora(f.Hasta)}"))})";
        string Tarifa(ZonaDef z) => $"{z.Nombre} ${z.Estandar:0} estándar" + (z.Urgente is { } u ? $" / ${u:0} urgente" : "");
        var pendientes = p.Accion switch
        {
            AccionBultosPendientes.NuevaVisitaSinRecargo => "nueva visita sin recargo",
            AccionBultosPendientes.NuevaVisitaConRecargo => $"nueva visita con recargo de ${p.ImporteRecargo:0}",
            _ => "devolución al comercio",
        };
        var plazos = $"estándar {p.Estandar.Horas} h (corte {p.Estandar.Corte:HH\\:mm})" +
                     (p.Urgente is { } u2 ? $", urgente {u2.Horas} h (corte {u2.Corte:HH\\:mm})" : ", sin servicio urgente");
        return
        [
            $"Zonas y franjas: {string.Join("; ", p.Zonas.Select(Zona))}",
            $"Tarifas v1: {string.Join("; ", p.Zonas.Select(Tarifa))}; recargo del {p.RecargoEstandarPct:0} % sobre el estándar",
            $"Reglas de entrega v1: {p.MaxIntentos} intentos ({p.HorasEntreIntentos} h entre intentos); pruebas: {string.Join(" + ", p.Pruebas)}; entrega parcial permitida; bultos pendientes: {pendientes}",
            $"Plazos de servicio: {plazos}",
            $"Devolución: {p.DiasEnDeposito} días en depósito, plazo máximo {p.PlazoDevolucionDias} días, " +
                (p.DevolverAlAgotar ? "devuelve al agotar los intentos" : "no devuelve automáticamente al agotar intentos"),
            $"Motivos de no entrega: {string.Join("; ", motivos.Select(m => $"{m.Nombre}{(m.EsReintentable ? " (reintentable)" : "")}"))}",
            $"Hoja de ruta del repartidor ({p.Zonas.Length} zonas): visita pendiente; entrega parcial " +
                $"({p.EntregadosParcial} de {p.BultosParcial} bultos entregados); no entregado con aviso fallido " +
                $"({p.IntentosAvisoFallido} intentos); entregado",
        ];
    }
}
