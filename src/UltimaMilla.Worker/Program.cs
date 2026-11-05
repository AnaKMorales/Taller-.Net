using UltimaMilla.Application.Mensajeria;
using UltimaMilla.Infrastructure.Avisos;
using UltimaMilla.Infrastructure.Mensajeria;
using UltimaMilla.Infrastructure.Persistence.Outbox;
using UltimaMilla.Worker;

var builder = Host.CreateApplicationBuilder(args);
// Clave: RabbitMq__Clave (variable de entorno) o user-secrets. Nunca en appsettings.
builder.Services.AgregarTransporteRabbitMq(builder.Configuration);
builder.Services.AddHostedService<InicializadorTopologia>();

// Cadena de conexión: ConnectionStrings__UltimaMilla (variable de entorno o user-secrets).
builder.Services.AgregarPublicacionOutboxPorOperador(
    builder.Configuration.GetConnectionString("UltimaMilla")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:UltimaMilla."));
builder.Services.AddSingleton(builder.Configuration.GetSection(OpcionesPublicacionPeriodica.Seccion)
    .Get<OpcionesPublicacionPeriodica>() ?? new OpcionesPublicacionPeriodica());
builder.Services.AddHostedService<PublicacionOutboxWorker>();

// Consumo de "avisos": tenant por mensaje, idempotencia (MensajeId, Consumidor) y manejador del evento.
builder.Services.AgregarConsumoMensajes();
// Webhooks firmados a las suscripciones del comercio. Un fallo dispara el reintento por las colas de espera.
builder.Services.AgregarEntregaWebhooks(builder.Configuration.GetSection(OpcionesWebhook.Seccion).Get<OpcionesWebhook>());
builder.Services.AddSingleton<IManejadorMensajes, ProcesadorAvisos>();
builder.Services.AddHostedService<ConsumidorAvisosWorker>();

var host = builder.Build();
host.Run();
