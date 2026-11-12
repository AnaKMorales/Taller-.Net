# Pruebas de resiliencia (entorno local)

Qué pasa cuando se cae una dependencia y si el sistema se recupera solo al volver. Se prueba
**solo en local**, con `deploy/compose.yaml`. No aplica a AWS ni a la instancia.

## Cómo correrlas

```bash
cd deploy
WEB_ENTORNO=Development WEBHOOKS_PERMITIR_DESTINOS_PRIVADOS=true \
  docker compose --env-file .env --profile app up -d --build
cd ..
./scripts/resiliencia.sh                 # pide confirmación antes de empezar
```

- Apaga **de a un servicio por vez**, en este orden: redis, rabbitmq, postgres y receptor.
  Solo usa `docker compose stop`/`start`: no borra contenedores ni volúmenes.
- Si se corta en la mitad (Ctrl+C o un error), un `trap` vuelve a prender los cuatro servicios.
- La salida completa se guarda **fuera del repo** (por defecto `~/ultimamilla-resiliencia-<fecha>.log`;
  se puede cambiar con `SALIDA=`). El script se niega a escribirla dentro del repositorio.
- Usa los datos demo (`POST /dev/datos-demo`), entra como administrador del operador B
  (`OPERADOR=1`, por defecto; `OPERADOR=0` usa el A) y suscribe el receptor de prueba (`/ok`, `/flaky?n=2`, `/fail`) al evento `Admitir`. Las acciones
  de prueba son:
  - **seguimiento:** `GET /api/seguimiento/{token}` (público, cacheado en Redis 30 s);
  - **alta:** `POST /dev/simular/alta`, que crea un envío y deja un mensaje en el outbox;
  - **webhook:** que el receptor anote un `POST /ok` respondido con 2xx.
- Cada chequeo sale como `OK` o `DISTINTO`, y al final hay un resumen. Duración aproximada: 8 a 10 min.

## Resultados

"Qué se esperaba" sale de leer el código y los ADR. "Qué pasó" sale de la **segunda corrida**, del
2026-11-11 entre las 23:35 y las 23:44 UTC (operador demo B), registrada en
`~/ultimamilla-resiliencia-20261111-203522.log`. La primera corrida (operador A, 23:25-23:34 UTC,
`~/ultimamilla-resiliencia-20261111-202536.log`) dio los mismos resultados en los chequeos de Redis,
`/ready`, `/live` y seguimiento.

**Atención:** durante estas corridas, la acción de prueba "alta" (`POST /dev/simular/alta`) devolvía
**500 también con todo prendido**, por un error en el código de demo y no por la caída (ver
"Error de la demo" más abajo, ya corregido). Por eso todo lo que depende de un alta nueva
(outbox → RabbitMQ → Worker → webhook) quedó **sin probar** y está marcado como *No concluyente*
hasta la próxima corrida.

| Qué se corta | Qué se esperaba (según el código y los ADR) | Qué pasó | ¿Coincidió? |
| --- | --- | --- | --- |
| **Redis**: seguimiento | Responde **200** leyendo de la base. La caché nunca hace fallar una consulta (ADR-004; `Infrastructure/Cache/CacheSegura.cs`). | 200 en las dos consultas, leyendo de la base. La Web logueó un warning: "No se pudo leer la caché \"seguimiento\"; se consulta la base." (`RedisConnectionException`). | Sí |
| **Redis**: errores y pausa | Se cuenta un error en `ultimamilla.cache.errores` y la Web loguea un warning (`CacheSegura.cs:78`). Durante 30 s no se vuelve a intentar Redis (`CacheSegura.PausaTrasFalla`). | El contador de errores de la caché subió (0→1 en la 1.ª corrida y 1→2 en la 2.ª). Se registró **un solo** warning con dos consultas seguidas, lo que es coherente con la pausa de 30 s. La duración exacta de la pausa no se midió. | Sí |
| **Redis**: `/ready` | **200**. `/ready` solo revisa PostgreSQL (`Web/Program.cs:60-61, 217`). | 200. | Sí |
| **Redis**: al volver | Pasada la pausa de 30 s, la caché vuelve a guardar y a acertar sin reiniciar la Web. | Redis quedó `healthy` en ~5 s. Después de esperar 35 s, el seguimiento devolvió 200 y los aciertos de la caché subieron (2→3) sin reiniciar la Web. | Sí |
| **RabbitMQ**: alta de envío | **204**. El envío y su mensaje del outbox se guardan en la misma transacción en PostgreSQL. La Web no publica a RabbitMQ, lo hace el Worker (ADR-006). | 500, pero por el error de la demo (`NullReferenceException` en `LineaTarifa.ZonaId`), igual que con todo prendido. No se creó ningún envío. | No concluyente |
| **RabbitMQ**: Worker | `PublicacionOutboxWorker` loguea "Falló la publicación del operador…" en cada ciclo (2 s) y sigue. Los mensajes quedan pendientes (`PublicadoEn` vacío). No llegan webhooks. | El Worker logueó "No se pudo actualizar la muestra de \"rabbitmq\"" (`BrokerUnreachableException`, de las métricas) y siguió corriendo. No apareció "Falló la publicación del operador…": como no hubo altas, el outbox no tenía nada que publicar. No llegaron webhooks. | No concluyente |
| **RabbitMQ**: `/ready` | **200** (no revisa RabbitMQ). | 200. | Sí |
| **RabbitMQ**: al volver | La conexión se recupera sola (`AutomaticRecoveryEnabled = true`, `ConexionRabbitMq.cs:30`), el outbox publica lo pendiente y el consumidor vuelve a leer `avisos`: llega el webhook `/ok`. [NO VERIFICADO: que el consumidor se reenganche solo sin reiniciar el Worker; depende de la recuperación automática del cliente de RabbitMQ.] | RabbitMQ quedó `healthy` en ~6 s y el Worker siguió vivo. No llegó ningún webhook en 120 s, pero tampoco había un alta pendiente. La reconexión del consumidor sigue sin verificar. | No concluyente |
| **PostgreSQL**: `/live` | **200**: el proceso sigue vivo (`/live` no corre chequeos). | 200. | Sí |
| **PostgreSQL**: `/ready` | **503**: falla `PostgresHealthCheck`. | 503. La Web logueó `Health check "postgresql"` con `57P01: terminating connection due to administrator command`. | Sí |
| **PostgreSQL**: seguimiento y alta | **5xx**: no se puede leer ni guardar. El script espera 31 s antes, para que el seguimiento no salga de la caché. [NO VERIFICADO: la sesión por cookie se valida contra la base en cada pedido (`TenantResolver`), así que el alta podría devolver otro código (401, 403 o una redirección) en lugar de 5xx.] | Seguimiento: 500. Alta: 500 por la base caída (esta vez no fue el error de la demo, que no llega a ejecutarse). No hubo 401, 403 ni redirección. | Sí |
| **PostgreSQL**: Worker | Loguea errores al listar operadores con pendientes y lo reintenta en el próximo ciclo (`PublicacionOutboxWorker.cs:29-31`). | En la ventana de logs, el Worker logueó 8 veces "No se pudo listar los operadores con mensajes pendientes; se reintenta en el próximo ciclo." y siguió vivo. | Sí |
| **PostgreSQL**: al volver | `/ready` vuelve a 200, la misma sesión sigue sirviendo (claves de Data Protection en el volumen `claves_data`) y el alta nueva genera su webhook. El pool de Npgsql abre conexiones nuevas solo. No hay `EnableRetryOnFailure`, así que los pedidos que fallaron no se reintentan. | Postgres quedó `healthy` en ~7 s; `/ready` y el seguimiento volvieron a 200 sin reiniciar nada. La sesión siguió valiendo (el alta llegó al código, no dio 401). El alta dio 500 por el error de la demo, así que el webhook no se pudo probar. | Parcial (lo probado coincide; el webhook es no concluyente) |
| **Receptor**: alta de envío | **204**. Guardar el envío no depende del receptor. | 500 por el error de la demo. No se creó ningún envío. | No concluyente |
| **Receptor**: Worker | El POST falla ("Error de conexión"), se guarda el intento en `EntregaAviso` y el mensaje pasa a `avisos.espera.5s` y después a `avisos.espera.30s` (`PoliticaReintentos`). | Sin alta no hubo webhook que intentar: no hay intentos fallidos en los logs. | No concluyente |
| **Receptor**: al volver | Si vuelve antes de ~2 min 35 s, el aviso a `/ok` llega en un **reintento** (intento > 1) y no va a la DLQ. El historial del receptor está en memoria y se borra al reiniciarlo. | El receptor volvió en ~0 s (`running`). No llegó ningún `/ok` en 90 s, pero no había ningún aviso pendiente. | No concluyente |
| **Receptor**: `/fail` | Siempre responde 500, así que ese aviso termina en `avisos.dlq` y se ve en `/backoffice/fallidos`. No indica una falla de resiliencia. | No se generaron avisos nuevos. | No concluyente |

## Error de la demo en `/dev/simular/alta` (corregido)

Era un error del código de demo de la Web (`src/UltimaMilla.Web/Desarrollo/DatosDemo.cs`,
`SimularAltaAsync`), no de resiliencia. Es una regresión del commit `8f7b043` (2026-11-08), que pasó
`/dev/datos-demo` al escenario de dos operadores (`EscenarioDemo`). `SimularAltaAsync` seguía escrito
para el escenario anterior, de un operador con una zona y una franja.

| Operador demo | Error | Causa |
| --- | --- | --- |
| A (`operadores[0]`) | `InvalidOperationException: Sequence contains more than one element` en `DatosDemo.cs:176` | `NuevoEnvio` usa `zona.Franjas.Single()`, pero la zona "Centro" del escenario tiene dos franjas (Mañana y Tarde). |
| B (`operadores[1]`) | `NullReferenceException` en `LineaTarifa.ZonaId` (`Domain/Configuracion/LineaTarifa.cs:16`), llamado desde el constructor de `Envio` (`Envio.cs:80`) | `SimularAltaAsync` cargaba el cuadro con `.Include(c => c.Lineas)` pero sin la zona de cada línea. EF completa `LineaTarifa.Zona` solo con las zonas que ya están en memoria, y la consulta carga una sola (`FirstAsync`). Con una zona por operador no se notaba; con dos, las líneas de la otra zona quedan con `Zona = null`. |

Arreglo (solo en `SimularAltaAsync`): se usa la primera franja de la zona, ordenada por `Desde`,
y se agrega `.ThenInclude(l => l.Zona)` a las líneas del cuadro. Lo cubre la prueba de integración
`PostgresTests.SimularAltaFuncionaConLosDosOperadoresDelEscenarioDemo`, que hace un alta con el
operador A y otra con el B. Sin el arreglo, falla.

Pendiente: volver a correr `scripts/resiliencia.sh` para completar las filas *No concluyente*.

## Notas

- **Observación fuera del script (2026-11-12, ~01:50 UTC):** después de las corridas, la cola `avisos`
  tenía 88 mensajes y **0 consumidores**. El Worker seguía en marcha (no se había reiniciado desde
  antes del corte de RabbitMQ) pero ya no consumía. Con `docker compose restart worker` volvió a
  consumir y `scripts/demo-webhooks.sh` mostró `/ok`, `/flaky` y `/fail`. Esto indica que el
  consumidor **no se reengancha solo** después de un reinicio de RabbitMQ (la fila "RabbitMQ: al
  volver"), aunque la publicación sí se recupera. La fila sigue *No concluyente* hasta confirmarlo con
  `scripts/resiliencia.sh`. El código está en `Infrastructure/Mensajeria` (`ConsumidorRabbitMq`,
  `ConexionRabbitMq`) y no se modificó.

- El escenario "Web caída" no está incluido: con la Web apagada no hay acción de prueba. La PWA
  sigue trabajando sin conexión por diseño (ADR-005).
- Si un chequeo sale `DISTINTO`, anotá en "Qué pasó" el código obtenido y las líneas de log que lo
  explican. Están en el archivo de salida, en los bloques "Logs de web y worker".
