using UltimaMilla.Infrastructure.Notificaciones;
using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Infrastructure.Avisos;
using UltimaMilla.Infrastructure.Mensajeria;
using UltimaMilla.Infrastructure.Persistence.Outbox;
using UltimaMilla.Worker;
using UltimaMilla.Worker.Observabilidad;
using UltimaMilla.Infrastructure.Observabilidad;
using UltimaMilla.Application.Backoffice;
using UltimaMilla.Infrastructure.Persistence;
using UltimaMilla.Infrastructure.Persistence.Operacion;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AgregarTelemetriaWorker();
// Clave: RabbitMq__Clave (variable de entorno) o user-secrets. Nunca en appsettings.
builder.Services.AgregarTransporteRabbitMq(builder.Configuration);
builder.Services.AddSingleton(sp => new SondeoMetricasOperativas(
    builder.Configuration.GetConnectionString("UltimaMilla")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:UltimaMilla."),
    sp.GetRequiredService<ConexionRabbitMq>(), sp.GetRequiredService<OpcionesRabbitMq>()));
builder.Services.AddHostedService<InicializadorTopologia>();
builder.Services.AddHostedService<MetricasWorker>();

// Cadena de conexión: ConnectionStrings__UltimaMilla (variable de entorno o user-secrets).
builder.Services.AgregarPublicacionOutboxPorOperador(
    builder.Configuration.GetConnectionString("UltimaMilla")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:UltimaMilla."),
    builder.Configuration.GetSection(OpcionesPublicacionPeriodica.Seccion).Get<OpcionesPublicacionOutbox>());
builder.Services.AddSingleton(builder.Configuration.GetSection(OpcionesPublicacionPeriodica.Seccion)
    .Get<OpcionesPublicacionPeriodica>() ?? new OpcionesPublicacionPeriodica());
builder.Services.AddHostedService<PublicacionOutboxWorker>();

// Consumo de "avisos": tenant por mensaje, idempotencia (MensajeId, Consumidor) y manejador del evento.
builder.Services.AgregarConsumoMensajes();
// Webhooks firmados a las suscripciones del comercio. Un fallo dispara el reintento por las colas de espera.
builder.Services.AgregarEntregaWebhooks(builder.Configuration.GetSection(OpcionesWebhook.Seccion).Get<OpcionesWebhook>());
// Aviso al destinatario en cada cambio de estado relevante; corre antes de los webhooks (docs/notificaciones-destinatario.md).
builder.Services.AgregarNotificacionesDestinatario(builder.Configuration.GetSection(OpcionesNotificaciones.Seccion)
    .Get<OpcionesNotificaciones>(),
    // Correo real: Smtp__Servidor, Smtp__Remitente, Smtp__Usuario y Smtp__Clave (env/user-secrets). Sin servidor, solo log.
    builder.Configuration.GetSection(OpcionesSmtp.Seccion).Get<OpcionesSmtp>());
builder.Services.AddSingleton<IManejadorMensajes, ProcesadorAvisos>();
builder.Services.AddHostedService<ConsumidorAvisosWorker>();

builder.Services.AddSingleton<IProcesamientoOperativo>(sp => new ProcesamientoOperativo(
    sp.GetRequiredService<DbContextOptions<AppDbContext>>(), sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(builder.Configuration.GetSection("CierreOperativo").Get<OpcionesCierreOperativo>() ?? new());
builder.Services.AddHostedService<CierreOperativoWorker>();

var host = builder.Build();
host.Run();
