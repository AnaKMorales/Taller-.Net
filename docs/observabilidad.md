# Observabilidad

Este apartado corresponde al punto 4 de la organización del equipo: logs estructurados,
métricas, trazas correlacionadas y comprobaciones de salud. Usa Serilog, OpenTelemetry y
System.Diagnostics.Metrics. Domain y Application conservan sus dependencias actuales.

## Recorrido de una acción

1. Al registrar una visita, la PWA genera un contexto W3C `traceparent` y lo guarda junto
   con la acción en IndexedDB, antes del primer envío. Una acción pendiente de una versión
   anterior recibe el contexto y lo guarda antes de intentar enviarse.
2. ApiRepartidorHttp envía ese contexto en la cabecera de la petición de intento. CORS
   permite la cabecera desde los orígenes de la PWA configurados en Web.
3. ASP.NET Core inicia la actividad de servidor con esa traza. Los logs de la petición
   incluyen método, plantilla de ruta, respuesta, duración, aplicación, ambiente y TraceId.
4. El mensaje del outbox captura TraceParent y TraceState como metadatos opcionales en
   columnas propias. La operación de negocio y su mensaje siguen guardándose juntos.
5. El publicador conserva el contexto original y lo envía en cabeceras RabbitMQ. Los
   contratos JSON V1 y V2 mantienen su formato. La migración TraceContextOutbox agrega
   columnas anulables; los mensajes anteriores pueden seguir sin contexto.
6. El consumidor recupera exclusivamente el contexto del mensaje e inicia una actividad
   `avisos process`. Sus logs incluyen el identificador de mensaje y el de traza. Los
   reintentos y la DLQ conservan las cabeceras del mensaje. Un contexto ausente o inválido
   genera una traza nueva y no hereda una actividad ajena del hilo consumidor.

La correlación identifica un recorrido técnico. La autorización, el operador, la cuenta,
la idempotencia y las versiones de concurrencia siguen resolviéndose por sus mecanismos
de negocio. La PWA origina el contexto; los tiempos exportados corresponden a las
actividades instrumentadas de API, consumidor y llamadas HTTP.

## Logs y exportación

Web y Worker usan Serilog con JSON en consola. Los niveles se configuran en
`Serilog:MinimumLevel`. Los mensajes de ILogger pasan por ese proveedor.
Con `OpenTelemetry:ExportarOtlp=true`, Serilog.Sinks.OpenTelemetry también envía logs al
receptor OTLP. El SDK de OpenTelemetry exporta métricas y trazas al mismo receptor.
Los recursos identifican `UltimaMilla.Web` y `UltimaMilla.Worker` y su ambiente.

El resumen HTTP usa la plantilla de ruta y omite la consulta para proteger el token del
seguimiento público. Antes de exportar trazas HTTP se elimina la URL completa y sus
parámetros; en las entrantes queda la plantilla de ruta. Los logs automáticos de
HttpClientFactory se limitan a Warning para evitar sus mensajes informativos con URLs.
Las métricas técnicas no usan identificadores de envíos, tokens o usuarios como etiquetas.
No se habilita el registro de cuerpos de peticiones ni credenciales.

## Métricas disponibles

El SDK recoge tres grupos propios, además de las instrumentaciones HTTP:

| Medición | Origen y significado |
| --- | --- |
| Tiempo medio, p95 y p99 HTTP | Histograma http.server.request.duration de ASP.NET Core, con límites explícitos de 5 ms a 10 s. Incluye cantidad, suma y buckets. |
| Errores HTTP | Respuestas 5xx sobre el total de peticiones. Se calcula en ventanas de cinco minutos. |
| Caché | Meter UltimaMilla.Cache: contadores de aciertos, fallos y errores. Grafana separa tarifario y seguimiento. Para cada uno muestra aciertos, fallos y la tasa aciertos / (aciertos + fallos). |
| Outbox pendiente | Cantidad de filas sin PublicadoEn, obtenida de PostgreSQL. |
| Publicaciones | Contadores de publicaciones confirmadas y errores del publicador. Un reenvío confirmado puede aumentar nuevamente el contador. |
| Avisos fallidos | EntregasAviso con Exitoso=false e Intentos>0. Incluye avisos que todavía pueden reintentarse. |
| Profundidad de colas | Mensajes listos en avisos, DLQ y las tres colas de espera. No incluye mensajes entregados al consumidor y todavía sin ack. |
| Procesamiento de mensajes | Contador e histograma por resultado del manejador. Incluye reentregas; no representa eventos únicos. |
| Sincronizaciones pendientes/rechazadas | Suma de las últimas cantidades reportadas por los dispositivos activos. |
| Dispositivos informando | Cantidad de pares usuario/dispositivo con muestra vigente. Se exporta solo el total. |
| Antigüedad de las muestras | Tiempo desde el último sondeo completo correcto de PostgreSQL y RabbitMQ. |

SondeoMetricasOperativas realiza únicamente conteos técnicos globales y declaraciones
pasivas de colas. MetricasWorker lo ejecuta cada 30 segundos, con límite de cinco segundos
por dependencia. Cada dependencia se trata por separado. Si falla, se registra el error
y la siguiente ronda vuelve a intentarlo. No se sustituye un fallo por un cero inventado.
La fecha de la última muestra permite reconocer valores antiguos.

## Reportes de la PWA

Antes y después de sincronizar, la PWA llama a `POST /api/repartidor/telemetria` con
Bearer de repartidor y estos datos: DispositivoId, Pendientes y Rechazadas. El servidor
resuelve la identidad autenticada y acepta cantidades entre cero y 100000 y un identificador
no vacío. Una solicitud válida devuelve 204; datos inválidos, 400. Se admiten hasta 10000
muestras activas; una nueva por encima del límite devuelve 429.

El identificador de dispositivo se guarda localmente. La muestra de un mismo usuario y
dispositivo reemplaza a la anterior y vence a los cinco minutos. Las muestras son memoria
técnica del proceso Web y se pierden al reiniciarlo. Sus identificadores nunca salen como
etiquetas métricas. El reporte tiene un plazo de dos segundos; una falla de red, sesión,
servidor o almacenamiento auxiliar no impide intentar enviar la entrega.

Esta medición muestra el último reporte recibido. Un dispositivo desconectado no puede
informar sus cambios actuales; al vencer su muestra deja de contribuir al total. No es un
inventario completo de todos los pendientes offline. En despliegues con varias Web se
debe revisar la agregación de estas muestras entre procesos; el alcance obligatorio del
equipo de tres permite una sola instancia de API.

## Uso local

El compose `deploy/observabilidad/compose.yaml` usa grafana/otel-lgtm con versión fijada.
Incluye OpenTelemetry Collector, Grafana, Prometheus, Tempo y Loki para demostración y
desarrollo. Provisiona las fuentes y el tablero desde archivos del repositorio.
Es para desarrollar desde el IDE; con el stack completo ver más abajo. Sus puertos se publican solo
en localhost y el contenedor no tiene volumen persistente para retener las mediciones al
recrearlo.

```powershell
docker compose -f deploy/observabilidad/compose.yaml up -d
$env:OpenTelemetry__ExportarOtlp = "true"
$env:OTEL_EXPORTER_OTLP_ENDPOINT = "http://localhost:14317"
$env:OTEL_EXPORTER_OTLP_PROTOCOL = "grpc"
```

Iniciar Web y Worker desde esa terminal con sus conexiones habituales. La PWA no requiere
credenciales del colector: transmite contexto a la API y reporta sus cantidades a su
endpoint autenticado. La exportación queda deshabilitada por defecto.

| Dirección local | Uso |
| --- | --- |
| http://localhost:3300/d/ultimamilla-tecnico | Tablero técnico de Grafana. |
| http://localhost:14317 | Receptor OTLP gRPC. |
| http://localhost:14318 | Receptor OTLP HTTP, si se configura ese protocolo. |
| http://localhost:19090 | Consulta de métricas Prometheus. |
| http://localhost:13200 | API de trazas de Tempo. |

En Grafana, Explore permite consultar Tempo por TraceId y los logs de Loki, por ejemplo:

```logql
{service_name=~"UltimaMilla.Web|UltimaMilla.Worker"} | trace_id="identificador-de-traza"
```

Loki tiene un campo derivado TraceId que enlaza con Tempo. Los paneles de tasas y
percentiles necesitan peticiones y al menos dos muestras de los contadores; después de
arrancar pueden aparecer sin datos. La última muestra de cada dependencia ayuda a
distinguir una medición antigua de un estado actual.

Para detener el tablero:

```powershell
docker compose -f deploy/observabilidad/compose.yaml down
```

## Con el stack completo y en AWS

`deploy/compose.yaml` incluye el servicio `lgtm` (misma imagen y versión) en el perfil `app`, así que se
despliega en la EC2 junto con Web y Worker. Web y Worker exportan con `OpenTelemetry__ExportarOtlp=true` y
`OTEL_EXPORTER_OTLP_ENDPOINT=http://lgtm:4317` por la red interna; el colector no publica puertos hacia afuera.
Mediciones en el volumen `lgtm_data` (sobreviven a un redespliegue) y límite de 1,5 GB de memoria.

Grafana sale por Traefik en `/grafana` (en AWS `https://<dominio>/grafana/`, mismo certificado). Usuario `admin`,
clave `GRAFANA_ADMIN_PASSWORD`: en local en `deploy/.env`; en AWS la genera Terraform (módulo `parametros`) en SSM:

```bash
aws ssm get-parameter --with-decryption --name /ultimamilla/dev/GRAFANA_ADMIN_PASSWORD --query Parameter.Value --output text
```

La imagen trae acceso anónimo como Admin por defecto: se apaga (`GF_AUTH_ANONYMOUS_ENABLED=false`) porque en AWS
queda expuesto a internet. El pipeline y `desplegar-manual.sh` suben `deploy/observabilidad/provisioning` y
`dashboards` al bucket junto con el compose, y `desplegar.sh` verifica que `lgtm` quede corriendo.
Si `lgtm` se cae, Web y Worker siguen funcionando: solo se pierden mediciones de ese lapso.

No levantar a la vez este compose y `deploy/observabilidad/compose.yaml` (el de desarrollo desde el IDE):
los dos usan el puerto local 3300.

## Salud

Web expone /live, /ready y /health sin autenticación. /live comprueba que el proceso
responde. /ready y /health conectan a PostgreSQL y ejecutan SELECT 1 con límite de tres
segundos. Devuelven Healthy/200 o Unhealthy/503 sin detalles internos y sin caché.
No verifican Redis, RabbitMQ ni la aplicación de migraciones; estas se aplican mediante
el procedimiento de arranque del proyecto.

## Pruebas

Las pruebas verifican logs sin el token del seguimiento, métricas y trazas HTTP, cabecera
en una llamada saliente real, salud con PostgreSQL disponible y caído, migración de mensajes
anteriores, conservación del contexto en reintentos, cabeceras RabbitMQ y aislamiento de
actividades del consumidor. La PWA prueba persistencia, reenvío tras perder la respuesta,
actualización de acciones antiguas y sincronización aunque falle el reporte auxiliar.

Las pruebas contra PostgreSQL y RabbitMQ mantienen sus variables habituales. El caso
`ColectorRealRecibeTrazaPwaApiOutboxRabbitYMetricasDelTablero` se habilita además con
`ULTIMAMILLA_TEST_COLECTOR=true` y las variables OTLP anteriores, con el compose levantado.
Permite sustituir las direcciones de consulta mediante ULTIMAMILLA_TEST_TEMPO,
ULTIMAMILLA_TEST_PROMETHEUS y ULTIMAMILLA_TEST_GRAFANA.

Con esas variables y los tres servicios disponibles, desde la raíz:

```powershell
dotnet test tests/Integration/UltimaMilla.Infrastructure.IntegrationTests -c Release --filter "FullyQualifiedName~ColectorReal"
```

Ese caso usa el cliente y sincronizador de la PWA, la API real, PostgreSQL, el publicador,
RabbitMQ y el consumidor. Consulta la traza en Tempo y los logs correlacionados en Loki,
verifica métricas recibidas y ejecuta las consultas de los paneles en Prometheus.
El manejador de negocio del consumidor se sustituye en esa prueba para confirmar el
mensaje y capturar su contexto; no demuestra por sí sola la entrega de un webhook real.

En GitHub Actions se combina el compose base con `deploy/observabilidad/compose.ci.yaml`.
Loki guarda sus datos y su WAL (registro previo a la escritura) en un tmpfs limitado a
512 MiB. Se descartan al eliminar el contenedor de pruebas. Así la ingestión no depende
del espacio ocupado por imágenes y herramientas en el disco del runner. El WAL sigue
habilitado y conserva el umbral de protección del 90 %; no se desactiva la protección
ni se omite la comprobación de logs. Este ajuste solo se aplica al CI.

El workflow imprime el uso del disco del runner al iniciar y el del tmpfs al iniciar y
al terminar. En esta versión de Loki, el rechazo por espacio del WAL puede aparecer
como `Ingester is shutting down`, incluso si las consultas siguen respondiendo. Buscar
también `disk usage exceeded threshold, throttling writes` en los logs internos antes
de atribuirlo a un cierre del proceso. Véase la
[incidencia de Loki sobre ese mensaje](https://github.com/grafana/loki/issues/23769).

Referencias: [sink OTLP de Serilog](https://github.com/serilog/serilog-sinks-opentelemetry)
y [configuración de campos derivados de Loki](https://grafana.com/docs/grafana/latest/datasources/loki/configure/).
