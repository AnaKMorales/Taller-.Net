# UltimaMilla

Este repositorio está organizado siguiendo la estructura indicada en la documentación del proyecto.

El alcance, los pendientes y el orden de integración de la entrega parcial para el equipo
de tres están en [Seguimiento del cambio de requerimientos](CAMBIOS-REQUERIMIENTOS.md).

## Estado actual

La consulta de preparación y la API de despacho están documentadas en
[Despacho operativo](docs/despacho-operativo.md). La [API de carga por código](docs/carga-rutas.md)
también está disponible; las interfaces de carga y despacho siguen pendientes.

El backend de importación masiva idempotente y su contrato para integrar al portal se
describen en [Importación de envíos](docs/importacion-envios.md). La lectura de archivos
y la pantalla de importación siguen pendientes.

La solución usa .NET 10 y se trabaja con Rider. Actualmente existen Domain, Application,
Infrastructure, una base Web con Razor Pages y Blazor, pruebas unitarias y pruebas de integración
de persistencia y pruebas de arquitectura, la PWA del repartidor conectada a la API y el
Worker de mensajería. El despliegue se integra desde la infraestructura del equipo.

Domain contiene las entidades y relaciones de los cuatro diagramas UML, agrupadas por
área funcional. El detalle, las convenciones adoptadas y los límites de esta implementación
están en [Modelo de dominio](docs/domain-model.md).

Para compilar y ejecutar las pruebas desde la raíz del repositorio:

```powershell
dotnet build UltimaMilla.slnx
dotnet test UltimaMilla.slnx
```

En Rider se abre `UltimaMilla.slnx`; las pruebas se pueden ejecutar desde su explorador de tests.

Para medir la cobertura de líneas de Domain y Application desde PowerShell:

```powershell
./scripts/Test-Coverage.ps1
```

Cada capa debe superar el 70 %. GitHub Actions muestra ambos resultados en el resumen
del job y publica los XML de Cobertura en el artefacto `cobertura-domain-application`.

El job `build-test` levanta PostgreSQL, RabbitMQ y Redis temporales en el runner, además
del compose de observabilidad para la prueba de trazabilidad. No depende del despliegue
en AWS. Configura las conexiones de integración y publica los reportes TRX en
`resultados-pruebas`. `scripts/Assert-TestResults.ps1` hace fallar el job si encuentra
pruebas omitidas o incompletas, o si no hay resultados. Las credenciales de CI son
exclusivamente para esos contenedores efímeros. La primera corrida completa de esta
configuración debe validarse en Actions después de subir el cambio.

La prueba con colector real se ejecuta en un proceso independiente dentro de `build-test`,
con reporte `TestResults/colector/colector.trx` y control de omisiones propio. Continúa
siendo obligatoria. El CI habilita los logs internos de Loki y del colector para diagnóstico.

## Logs estructurados de Web y Worker

Web usa Serilog y escribe un objeto JSON por evento en la consola. Los mensajes de
`ILogger<T>` pasan por el mismo proveedor. Cada petición completada registra método,
plantilla de ruta, estado HTTP y duración en milisegundos, junto con la aplicación,
el ambiente y el identificador de traza.

Si el cliente envía un `traceparent` W3C válido, ASP.NET Core conserva su identificador
de traza y Serilog lo incluye en el registro de la petición. El outbox conserva el contexto
de esa actividad para la publicación diferida y RabbitMQ lo transporta en sus cabeceras.
El consumidor del Worker recupera esa traza para cada entrega y reintento.

La plantilla de ruta evita registrar el token público que forma parte de la URL de
seguimiento. El resumen de peticiones omite los parámetros de consulta.
Los niveles se configuran en `Serilog:MinimumLevel` dentro de appsettings o mediante
variables de entorno, por ejemplo `Serilog__MinimumLevel__Default=Debug`.

## Métricas y trazas de Web

OpenTelemetry instrumenta las peticiones de ASP.NET Core y las llamadas salientes de
HttpClient. También recoge los contadores existentes de `UltimaMilla.Cache`.
El servicio se identifica como `UltimaMilla.Web`, con el ambiente en
`deployment.environment.name`.

El histograma `http.server.request.duration` registra duración en segundos con ruta,
método y estado HTTP. Su cantidad, suma y distribución permiten construir tiempos medios,
percentiles y tasas de error en el tablero. Los contadores de caché permiten calcular la
tasa de aciertos. Las métricas no usan identificadores de envíos como etiquetas.

Para enviar métricas y trazas a un colector OTLP, configurar en el entorno de Web:

```powershell
$env:OpenTelemetry__ExportarOtlp = "true"
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:4317"
$env:OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
```

Dentro del compose, el endpoint debe apuntar al nombre del servicio colector accesible
desde Web. El exportador también admite la configuración estándar OTLP para endpoints
por señal y autenticación. La exportación queda deshabilitada por defecto en desarrollo.
Los logs de Serilog siguen saliendo como JSON en consola y se exportan por OTLP al habilitar
la misma opción. El compose local usa el puerto `14317` para OTLP gRPC.

Las trazas HTTP conservan `traceparent` y propagan el contexto en HttpClient.
Antes de exportarlas se elimina la URL completa y sus parámetros. En las peticiones
entrantes se conserva la plantilla de ruta para proteger el token del seguimiento.
La prueba captura trazas y métricas con un exportador en memoria y verifica una llamada
saliente real sobre loopback. El publicador conserva el contexto capturado en el outbox
cuando entrega el mensaje al transporte. Requiere aplicar la migración `TraceContextOutbox`
mediante el procedimiento de migraciones del proyecto. La PWA genera y guarda el contexto
con la acción antes de enviarla; lo conserva al reconectar y reenviar. Las acciones anteriores
reciben su contexto al sincronizar por primera vez con esta versión.

El Worker también usa Serilog con JSON en consola. OpenTelemetry identifica el servicio
como `UltimaMilla.Worker`, escucha la fuente `UltimaMilla.Mensajeria` e instrumenta sus
llamadas HttpClient. Se configura el exportador OTLP con las mismas variables que Web,
en el entorno del proceso Worker. Cada entrega recupera el contexto de sus cabeceras y
sus logs incluyen el identificador de mensaje y de traza. El contexto técnico no elige
el operador ni la cuenta comercial del mensaje.

## Tablero técnico local

El colector, Grafana, Prometheus, Tempo y Loki se pueden levantar juntos para desarrollo:

```powershell
docker compose -f deploy/observabilidad/compose.yaml up -d
$env:OpenTelemetry__ExportarOtlp = "true"
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:14317"
$env:OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
```

Después se inician Web y Worker desde esa terminal con sus conexiones habituales.
El tablero está en [Grafana local](http://localhost:3300/d/ultimamilla-tecnico).
Muestra tiempo medio, p95/p99 y errores HTTP, profundidad de colas, outbox pendiente,
avisos fallidos, tasa de aciertos de caché y sincronizaciones pendientes. Incluye logs
de Web y Worker y permite consultar trazas en Tempo.

El Worker toma muestras técnicas de PostgreSQL y RabbitMQ cada 30 segundos. La PWA reporta
su cola al sincronizar; el servidor mantiene las últimas muestras durante cinco minutos.
El reporte de telemetría es auxiliar y sus fallos no bloquean las entregas.
La profundidad de RabbitMQ cuenta mensajes listos, y la métrica de avisos cuenta intentos
persistidos que todavía no tuvieron éxito. El despliegue en AWS requiere configurar su
colector y las variables de los procesos desde la infraestructura del equipo.

Ver [funcionamiento, métricas, límites y pruebas de observabilidad](docs/observabilidad.md).

## Comprobaciones de salud de Web

Con Web levantada, se pueden consultar estas rutas sin iniciar sesión:

| Ruta | Qué comprueba |
| --- | --- |
| `/live` | El proceso Web responde. |
| `/ready` | PostgreSQL acepta una conexión y ejecuta `SELECT 1`. |
| `/health` | Todas las comprobaciones registradas. Actualmente incluye PostgreSQL. |

Devuelven 200 con `Healthy` cuando la comprobación pasa y 503 con `Unhealthy` si falla.
Una caída de PostgreSQL afecta a `/ready` y `/health`. `/live` permite comprobar que
Web sigue ejecutándose. Las respuestas públicas muestran solo el estado y evitan caché.
La comprobación de PostgreSQL tiene un límite de tres segundos y usa la conexión
`ConnectionStrings:UltimaMilla`. No consulta registros de operadores o comercios.

El despliegue puede usar `/live` para verificar el proceso y `/ready` para decidir si
enviarle tráfico. Esta comprobación verifica conectividad y consulta básica. Las migraciones
se aplican por el procedimiento de arranque antes de habilitar el servicio.

## Pruebas de arquitectura

`UltimaMilla.Architecture.Tests` usa NetArchTest.Rules y xUnit para comprobar la dirección
de dependencias entre las cuatro capas actuales y las referencias declaradas en sus proyectos.
Se ejecuta con la solución y no requiere Docker. Las entidades y casos de uso nuevos se
incluyen automáticamente; al agregar proyectos como Worker o Repartidor hay que ampliar
las reglas. Ver [ejecución y mantenimiento](docs/architecture-tests.md).

## Programación y escaneos en Domain

- El alta del envío recibe una `ProgramacionEntrega`: fecha, franja de su zona y ventana
  concreta. Las horas locales se conservan con su desplazamiento y los límites se consultan
  en UTC. Application debe resolver la zona horaria del operador; Domain no usa la del equipo.
- `Reprogramar` y `Aplazar` reciben una nueva programación, conservan la fecha comprometida
  original y registran la programación anterior y la nueva en el evento del envío.
- Los escaneos se crean con `RegistrarRecepcion`, `RegistrarCarga` o `RegistrarDevolucion`.
  La recepción no necesita ruta, la carga exige asignación activa a una ruta sin despachar,
  y la devolución se vincula a su proceso activo, con ruta de origen opcional.
- `RecibirEnDeposito` confirma el ingreso después de escanear todos los bultos.

Estos comportamientos se prueban en memoria. Infrastructure ya contiene los mapeos de
EF Core y las migraciones para PostgreSQL. Se validaron pruebas de integración contra
PostgreSQL local, incluidas las de aislamiento automático por operador. Envíos y rutas ya
cuentan con concurrencia optimista. El outbox ya registra mensajes de eventos en la misma
transacción; la publicación y la conexión del login siguen pendientes.

El núcleo ya registra intentos por bulto con resultados y evidencias, genera el estado
EntregadoParcialmente y reconoce reenvíos por contenido y parada de origen. La firma
tradicional conserva eventos V1; el método nuevo registra outbox V2. Los pendientes pueden
continuar en otra ruta con o sin recargo fijo, o devolverse al comercio según las reglas
versionadas. La capacidad y la devolución incluyen solo pendientes. El recargo se registra
una vez al despachar la visita y se conserva separado de la tarifa original; no representa
un pago realizado. Ver [políticas e integración](docs/politicas-pendientes.md).
Faltan endpoints e integración de las aplicaciones. Ver [contrato y límites](docs/intentos-por-bulto.md).

RegistrarIntentoHandler ya orquesta este registro desde Application: identidad autenticada,
catálogo del envío, versión esperada y guardado atómico. Los reenvíos iguales reconocen
el intento previo sin duplicar ni guardar de nuevo. Ver [integración del caso de uso](docs/registro-intento-aplicacion.md).

## Persistencia

Se configuraron EF Core 10, Npgsql y los mapeos de las 36 entidades. Se conservan los setters
privados de Domain; los constructores de rehidratación permiten recuperar objetos sin volver
a ejecutar sus reglas de alta. La migración `InitialCreate` está generada y se aplicó a la
instancia local de desarrollo. La migración posterior `OperatorIsolation` incorpora el tenant
y sus restricciones; cada integrante debe aplicar las migraciones pendientes en su propia base.

Estos comandos funcionan sin PostgreSQL ejecutándose:

```powershell
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/UltimaMilla.Infrastructure
dotnet ef migrations script --project src/UltimaMilla.Infrastructure --output docs/database-initial.sql
```

El manifiesto `dotnet-tools.json` fija la versión de `dotnet-ef`. La fábrica de diseño usa
`ConnectionStrings__UltimaMilla` si está definida; su valor predeterminado solo identifica
una base local, sin contraseña. Generar migraciones o SQL no abre una conexión.

El detalle de relaciones, índices, tipos y límites está en [Persistencia](docs/persistence.md).
También se conserva el [SQL de instalación desde cero para revisión](docs/database-initial.sql),
que incluye las nueve migraciones actuales.

## Concurrencia de envíos y rutas

`RowVersion` detecta modificaciones basadas en una versión anterior del envío o la ruta.
También se comprueban cambios en hijos, como paradas y escaneos. Dos asignaciones que
compiten por el mismo envío o la capacidad de una ruta no pueden confirmar ambas con
la misma versión. El intento perdedor se revierte y produce `ConflictoConcurrenciaException`;
debe consultarse el estado actual y revalidar la operación, sin reintentos automáticos.

La migración `OptimisticConcurrency` agrega las versiones a filas nuevas y existentes.
Los casos de uso deben cargar los agregados completos con seguimiento y guardar su unidad
de trabajo de forma atómica. Ver [funcionamiento, integración y límites](docs/concurrency.md).

## Calculador de tarifas

`ICalculadorTarifa` recibe el TarifarioDto existente, zona, modalidad y bultos. Application
suma medidas y delega a la política de Domain la selección del tramo y sus ajustes.
Devuelve precio base, recargos, bonificaciones, total redondeado y versión del cuadro.
No consulta datos ni cambia Web, caché o persistencia. Su conexión con el alta sigue pendiente.
Ver [reglas adoptadas, integración y límite actual del volumen](docs/tarifas.md).

## Configuración versionada de reglas y tarifas

`IConfiguracionVersionada` permite a un administrador activo consultar versiones y zonas,
obtener detalles y vigentes y guardar nuevas versiones completas. El operador se obtiene
del contexto autenticado. La versión esperada detecta cambios concurrentes; se permiten
vigencias inmediatas o futuras en UTC. Las versiones guardadas y sus hijos son inmutables
en SaveChanges; los envíos anteriores conservan sus referencias y tarifa.

Al confirmar un tarifario se solicita su invalidación mediante el contrato de caché existente.
La implementación, el registro opcional y los DTO están preparados para las pantallas;
Program ya registra el servicio y expone endpoints administrativos de reglas y tarifarios,
con permisos de administrador y antiforgery para escrituras. Los formularios siguen
requiriendo integración. Ver [contratos y límites](docs/configuracion-versionada.md).

## API de hoja de ruta y configuración

La API privada del repartidor permite consultar sus rutas despachadas y no rendidas en
`GET /api/repartidor/rutas` y descargar una en `GET /api/repartidor/rutas/{id}`. Las paradas
se ordenan por la secuencia Cod del despachador y contienen bultos de esa visita, versiones,
dirección, ventana y reglas para registrar entregas. No se agregó login móvil ni PWA.

La configuración se consulta y versiona en `/api/configuracion`, exclusivamente con perfil
Administrador. Las escrituras exigen cookie y token antiforgery. Endpoints, ejemplos de
integración y pendientes por responsable: [backend de los monitoreos](docs/api-monitoreos.md).

La migración `ReglasEntregaParcial` agrega el permiso de entrega parcial y la acción para
bultos pendientes a cada versión de reglas. PoliticasPendientesYRecargoFijo agrega el importe
fijo por visita y las relaciones de devolución y cargo. El núcleo aplica las tres políticas;
las pantallas y demás aplicaciones todavía requieren integración. El progreso está en
[CAMBIOS-REQUERIMIENTOS.md](CAMBIOS-REQUERIMIENTOS.md).

## Outbox transaccional

Cada evento nuevo de un envío genera automáticamente un mensaje pendiente en
`MensajesOutbox`. El cambio de negocio, el evento y el mensaje se guardan en la misma
transacción de PostgreSQL, tanto con SaveChanges como con SaveChangesAsync. Si falla
el guardado o hay un conflicto de concurrencia, no queda un mensaje de una operación fallida.

El mensaje usa un contrato JSON versionado con operador y cuenta comercial, sin datos
del destinatario ni token de seguimiento. Su identificador estable coincide con el evento.
La migración `TransactionalOutbox` agrega la tabla sin emitir eventos históricos.

`PublicadorOutbox` toma pendientes del operador con bloqueos FOR UPDATE SKIP LOCKED,
los entrega a `ITransporteMensajes` y completa PublicadoEn cuando ese contrato confirma.
Un fallo deja el mensaje pendiente; el mismo Id permite reconocer un reenvío. Requiere
un contexto interno dedicado y no ejecuta bucles ni tareas en segundo plano.

El adaptador real de RabbitMQ, la conexión desde el host, reintentos/backoff y la deduplicación
en el consumidor siguen pendientes. No se publica a RabbitMQ real en esta etapa.
Ver [Outbox e integración del publicador](docs/outbox.md) y [ADR-006](docs/adr/006-consistencia-outbox.md).

## Multitenancy por operador y cuenta comercial

Se sigue la estrategia por fila de la sección 11 y el ADR-002 del documento inicial.
Cada AppDbContext captura un TenantContext inmutable mediante ITenantActual: operador,
usuario/rol y cuenta activa para el perfil Comercio. Las consultas se filtran por operador
más cuenta; los usuarios internos mantienen su ámbito de operador según los permisos de API.
Sin contexto no se leen datos y el guardado se bloquea.

TenantSaveChangesInterceptor valida altas, modificaciones, bajas y referencias en ambos
niveles. TenantResolver resuelve y verifica claims autenticados y contextos de mensajes Worker.
ISeguimientoPublico resuelve un token aleatorio y devuelve un DTO limitado, en contexto de solo
lectura. TenantCacheKey separa las claves por operador/cuenta y perfil. Estos componentes no
implementan el login, el consumidor RabbitMQ ni el cliente Redis: sus proyectos deben conectarlos.

Comercio conserva su identidad compartida y las cuentas delimitan sus operaciones.
Los detalles, ejemplos de integración y límites están en [Multitenancy](docs/multitenancy.md)
y [ADR-002](docs/adr/002-multitenancy-por-fila.md).
La solución cuenta con 272 pruebas: 117 de Domain, 15 unitarias de Application,
37 unitarias de Infrastructure, 11 de arquitectura y 92 contra PostgreSQL,
incluidas nueve pruebas de concurrencia/migración, siete del registro transaccional del outbox
y nueve de integración de su componente publicador.

## PostgreSQL local para desarrollo

Con Docker Desktop ejecutándose, desde la raíz del repositorio:

```powershell
./scripts/Start-LocalPostgres.ps1
./scripts/Test-LocalPersistence.ps1
```

El primer script levanta PostgreSQL 17 en `127.0.0.1:55432`, crea credenciales aleatorias
en `deploy/.env.postgres` (excluido de Git) y espera a que esté listo. El segundo aplica
las migraciones y ejecuta las pruebas de integración en bases temporales separadas.
La base de desarrollo conserva sus datos en un volumen de Docker.

Este Compose es el apoyo local para probar persistencia. El despliegue completo con los
demás servicios sigue pendiente. Ver [comandos y conexión desde Rider](docs/postgres-local.md).
`dotnet test UltimaMilla.slnx` sigue funcionando sin Docker: omite las pruebas de
integración cuando no se configura su conexión; el segundo script sí las ejecuta.

## Caché con Redis

El tarifario vigente y el seguimiento público se cachean en Redis con dos perfiles distintos;
la decisión y sus límites están en [ADR-004](docs/adr/004-cache-e-invalidacion.md). Con Postgres
ya iniciado y Docker Desktop abierto, desde la raíz del repositorio:

```powershell
./scripts/Start-LocalRedis.ps1
./scripts/Start-Web.ps1
```

Redis queda en `127.0.0.1:56379`. `Start-Web.ps1` toma la contraseña de `deploy/.env.postgres`,
aplica las migraciones pendientes y levanta la Web en `http://localhost:5243`. Sin la cadena
`ConnectionStrings:Redis`, la Web usa caché en memoria.

Endpoints para probarla (los `/dev` existen solo en Development, porque todavía no hay login):

| Método | Ruta | Para qué |
| --- | --- | --- |
| POST | `/dev/datos-demo` | Crea **dos operadores con configuración distinta** (zonas, franjas, tarifas, reglas de entrega y motivos de no entrega). Cada uno deja un repartidor con una hoja de ruta con una visita pendiente, una entrega parcial por bulto y un envío no entregado con aviso fallido. Devuelve `operadores[]` con las cuentas (administrador, comercio, repartidor), los tokens de seguimiento y un resumen de lo que tiene de distinto cada uno. Sin cuerpo usa la clave de desarrollo `Demo1234!` (y la devuelve en `contrasena`); con `{"contrasena": "..."}` (mínimo 12 caracteres) usa esa clave y no la devuelve |
| POST | `/dev/datos-demo-pwa` | Un solo operador con una visita parcial pendiente, para probar la PWA |
| GET | `/api/seguimiento/{token}` | Seguimiento público, cacheado 30 s |
| GET | `/dev/tarifario/{operadorId}` | Tarifario vigente, cacheado hasta 30 min |
| POST | `/dev/tarifario/{operadorId}/invalidar` | Invalida el tarifario del operador |
| GET | `/dev/cache/metricas` | Aciertos, fallos, errores y tasa de aciertos por caché |

## Login, backoffice y portal

Con la Web levantada (`./scripts/Start-Web.ps1`, que también aplica las migraciones), crear datos con
`POST /dev/datos-demo` e ingresar en `http://localhost:5243/login` con el `emailAdministrador` o el
`emailComercio` y la `contrasena` de cualquiera de los dos operadores de `operadores[]`.
En AWS: `deploy/aws/sembrar-demo.sh` (ver `deploy/terraform/README.md`).

Hay un solo login para todos los roles, con email y contraseña. La contraseña se guarda como hash
(`PasswordHasher` de ASP.NET Identity, PBKDF2 con sal) en la columna `ContrasenaHash` de `Usuarios`,
configurada solo en Infrastructure: las clases de dominio no cambian. El login responde lo mismo si el
email no existe o la contraseña está mal. La sesión es una cookie con los claims que usa `TenantResolver`
(`operador_id`, rol y `cuenta_comercial_id`). Después del ingreso, cada rol va a su lugar:

| Rol | Destino |
| --- | --- |
| Administrador, Despachador, OperarioDeposito | `/backoffice/tablero` |
| Comercio | `/portal` (envíos de su cuenta comercial) |
| Repartidor | No ingresa a esta web: usa la aplicación del repartidor |

Pendiente para pasar a ASP.NET Identity completo: alta y cambio de contraseña desde la interfaz, bloqueo por
intentos fallidos y el token para la aplicación del repartidor.

| Ruta | Qué muestra |
| --- | --- |
| `/backoffice/tablero` | Envíos por estado y últimos cambios; se actualiza solo por SignalR (`/hubs/tablero`) |
| `/backoffice/incidencias` | Envíos no entregados, reprogramados, en devolución, extraviados o con discrepancias |
| `/backoffice/fallidos` | Avisos a comercios (webhooks) que no se pudieron entregar |
| `/portal` | Envíos de la cuenta comercial del usuario de comercio |

Cómo funciona el tiempo real: un interceptor de EF (`PublicarCambiosInterceptor`) toma cada `EventoDeEnvio`
nuevo y, solo si el guardado se confirmó, lo pasa al puerto `INotificadorTablero`. La Web lo envía por SignalR
únicamente al grupo del operador del envío. Cada conexión entra a su grupo según la identidad verificada con
`TenantResolver`, nunca por un dato que mande el navegador. En desarrollo, el tablero tiene botones para
simular un alta y una recepción en depósito y ver los cambios en vivo.

Límites actuales: el reintento manual de avisos fallidos se agrega cuando esté el Worker; con una transacción
explícita el aviso al tablero sale antes del commit (lo resuelve el outbox); el alta en Blazor (`/envios/nuevo`)
todavía no recibe el contexto del operador dentro del circuito interactivo.

## App del repartidor (PWA)

`src/UltimaMilla.Repartidor` es una PWA Blazor WebAssembly instalable: login, hoja de ruta con
paradas, registro de entregas exitosas (pruebas Nombre/Documento) y fallidas (motivo del catálogo),
cola local en IndexedDB y sincronización automática al volver la conexión. Cada acción lleva
`IdOperacion` generado en el dispositivo, `OcurridoEn` y GPS opcional. La barra superior indica
en línea / sin conexión / pendientes / rechazadas.

**El login es real**: `POST /api/repartidor/sesion` en Web valida con el mismo mecanismo que el
login web y entrega un token Bearer (8 h). El operador sale del token y `TenantResolver` lo verifica
en cada pedido. Con el token vencido o un 401, las acciones quedan pendientes hasta volver a ingresar.
La PWA descarga la hoja de ruta y sincroniza los intentos mediante la API de Web.
El login, su seguridad, el contrato, la política de conflictos
("el servidor gana") y el límite del GPS con pantalla apagada están en
[ADR-005](docs/adr/005-sincronizacion-offline-repartidor.md).
La lógica de cola y sincronización se prueba sin navegador en `tests/Unit/UltimaMilla.Repartidor.Tests`.

```powershell
./scripts/Start-Web.ps1 # Dejar Web en ejecución en una terminal
# En otra terminal, crear un escenario nuevo y arrancar la PWA
Invoke-RestMethod -Method Post http://localhost:5243/dev/datos-demo-pwa
dotnet run --project src/UltimaMilla.Repartidor --launch-profile http
```

La PWA llama a la API indicada en `src/UltimaMilla.Repartidor/wwwroot/appsettings.json` (`ApiUrl`);
Web solo acepta los orígenes de `Repartidor:Origenes` (en Desarrollo, `http://localhost:5086`).

Demostración (Chrome/Edge, DevTools → Network → Offline):

1. Ingresar en `http://localhost:5086/` con el `emailRepartidor` y la `contrasena` devueltos por el comando.
2. Abrir **Registrar visita** en **Entrega parcial de prueba**. Marcar **Offline** en DevTools.
3. Marcar `PARCIAL-01` como entregado y `PARCIAL-02` como no entregado con motivo **Ausente**.
   Adjuntar las fotos solicitadas, permitir el GPS y registrar la visita.
4. Volver a **Online** y comprobar que la visita pasa de pendiente a sincronizada.

Para abrir la app sin red después de recargar hace falta el service worker publicado
(`dotnet publish src/UltimaMilla.Repartidor -c Release` y servir la carpeta `wwwroot` publicada
en localhost); en `dotnet run` el service worker de desarrollo no guarda caché.


## Estructura

- `src/`
  - `UltimaMilla.Domain/` - entidades, reglas y máquina de estados.
  - `UltimaMilla.Application/` - casos de uso y contratos.
  - `UltimaMilla.Infrastructure/` - EF Core, Redis, RabbitMQ, outbox, Identity.
  - `UltimaMilla.Web/` - API, Razor Pages, Blazor y SignalR.
  - `UltimaMilla.Repartidor/` - Blazor WebAssembly PWA.
  - `UltimaMilla.Worker/` - consumidores y tareas en segundo plano.
- `tests/`
  - `Unit/`
  - `Integration/`
  - `Architecture/`
  - `Tenancy/`
- `deploy/` - Docker Compose y Terraform.
- `docs/`
  - `adr/` - registros de decisión de arquitectura.
- `scripts/` - scripts de automatización y soporte.

## Recomendaciones

El alta Web inicial todavía debe completar cuenta, configuración y bultos, conectar el
calculador de tarifa, preparar fechas y usar el contexto autenticado. `CrearEnvioHandler` usa el contrato
`IAltaEnvioRepository`, implementado en Infrastructure, para respetar la dirección de dependencias.
No se utiliza un operador vacío ni se omiten las reglas de Domain para guardar ese formulario.

Las migraciones del prototipo simplificado de Envio se conservaron en
[`docs/archive/envio-prototype-migrations`](docs/archive/envio-prototype-migrations/README.md).
EF solo utiliza el historial del modelo completo en `Infrastructure/Persistence/Migrations`.

1. Mantener la capa `Domain` libre de dependencias externas.
2. Usar `Application` para orquestar casos de uso.
3. Centralizar infraestructura y acceso a datos en `Infrastructure`.
4. Mantener pruebas separadas del código de producción.
5. Mantener la nomenclatura del dominio consistente con `UltimaMilla`.
