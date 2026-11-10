# ADR-004 Estrategia de caché e invalidación

Estado: implementado para el tarifario vigente y el seguimiento público.
Responsable del área: Ana Morales. Actualización: 30/10/2026.
Origen de la decisión: secciones 11.5 y 12.1 del documento inicial del Grupo 5 (Redis con dos perfiles).

## Contexto

Dos lecturas se repiten mucho y cambian poco. El cuadro tarifario del operador se consulta en cada
alta para calcular la tarifa, y solo cambia cuando el operador carga una versión nueva. El seguimiento
público lo consulta el destinatario, que recarga la página varias veces mientras espera la entrega.
Las dos van hoy a PostgreSQL en cada pedido. El documento inicial prevé Redis con políticas de
expiración e invalidación distintas, y claves separadas por operador y comercio.

## Decisión

Cache-aside sobre `IDistributedCache`: se busca en la caché; si no está, se lee de la base y se guarda.
La lógica vive en Infrastructure (`Cache/`) y los casos de uso usan los puertos de Application
(`ITarifarioVigente`, `IInvalidacionTarifario`, `ISeguimientoPublico`), así que no dependen de Redis.

| | Tarifario vigente | Seguimiento público |
| --- | --- | --- |
| Clave | `op:{operador}:tarifario:vigente` | `seguimiento:{SHA-256 del token}` |
| Duración | 30 min, o hasta que entre en vigor el próximo cuadro ya cargado, lo que ocurra antes | 30 s |
| Invalidación | Explícita con `IInvalidacionTarifario` al crear o activar una versión | Por evento: `InvalidarSeguimientoInterceptor` borra la entrada al guardar un cambio del envío |
| Desfase máximo visible | Ninguno si se invalida; si no, 30 min | Ninguno si se invalida; si la invalidación falla, 30 s |

- **Tarifario.** Es un dato del operador, no de un usuario: la clave usa `TenantCacheKey.PrefijoOperador`,
  y la lectura de la base usa un contexto acotado al operador, así un operador nunca recibe el
  tarifario de otro. Como un cuadro en uso es inmutable, la entrada solo queda vieja cuando hay
  otra versión; por eso se invalida en ese momento y además se limita a la próxima vigencia.
- **Seguimiento.** No se puede prefijar por operador porque el operador se conoce recién al resolver
  el token. No hace falta: el token es aleatorio y único en la plataforma, así que cada clave
  corresponde a un solo envío. Se guarda un hash del token y no el token, para que quien vea las
  claves de Redis no pueda consultar envíos. El DTO guardado es el mismo DTO público, sin datos internos.
  Se invalida en el momento del cambio: `InvalidarSeguimientoInterceptor` (un interceptor de
  `SaveChanges` del DbContext de la Web) anota los envíos con un evento nuevo, el envío modificado o
  un bulto modificado (entrega parcial), y después de confirmar el guardado borra sus entradas.
  Se invalida después y no antes de guardar: si se borrara antes, una consulta concurrente podría
  volver a cachear el estado viejo. Se eligió el interceptor y no el Worker porque es inmediato y no
  depende de que RabbitMQ esté disponible. Los 30 s quedan como red de seguridad: si Redis falla al
  invalidar, el guardado no se ve afectado y la entrada vieja vive como máximo esa duración.
- **No se guarda "no existe".** Así un envío o un tarifario recién creados se ven de inmediato.
- **Si Redis falla**, se lee de la base y se cuenta un error: la caché nunca hace fallar una consulta.
  Después de una falla, la caché se saltea durante 30 s, porque cada intento contra un Redis caído
  demora segundos.
- **Sin Redis configurado** se usa caché en memoria. Sirve para desarrollo y pruebas, no para más de
  una instancia.

## Métrica de aciertos

`CacheMetricas` publica los contadores `ultimamilla.cache.aciertos`, `ultimamilla.cache.fallos` y
`ultimamilla.cache.errores`, con la etiqueta `cache` (`tarifario` o `seguimiento`), en el Meter
`UltimaMilla.Cache`. OpenTelemetry y Prometheus recogen los contadores. Grafana muestra
aciertos, fallos y tasa de aciertos por caché. En Development también se pueden consultar
en `GET /dev/cache/metricas`.

## Alternativas consideradas

- **Solo `IMemoryCache` en el proceso.** Más simple, pero cada instancia tendría su propia caché y la
  invalidación del tarifario no llegaría a las demás. El documento inicial ya elige Redis.
- **`HybridCache` de .NET.** Agrega una capa en memoria delante de Redis y protege contra consultas
  simultáneas a la misma clave. Se descartó por ahora porque suma una segunda capa que también
  habría que invalidar; se puede adoptar después sin cambiar los puertos.
- **Caché de salida HTTP.** No sirve para el tarifario, que se usa dentro del alta y no como respuesta,
  y no permite invalidar por operador.

## Consecuencias y pendientes

- El caso de uso de alta debe pedir el tarifario a `ITarifarioVigente` (área de Agustín).
- La pantalla que cree o active un tarifario debe llamar a `IInvalidacionTarifario` (versionado editable, 29/10).
- Redis ya forma parte del entorno completo (`deploy/compose.yaml`). `deploy/compose.redis.yaml` sigue
  disponible para levantar solo Redis.
- Los endpoints `/dev/*` no tienen autenticación y existen solo en Development; se quitan o se protegen
  cuando esté el login.
- Pruebas: 7 unitarias (aciertos, operadores separados, invalidación, duración, Redis caído) y 3 de
  integración contra PostgreSQL real, una de ellas para la invalidación del seguimiento por evento.
- El Worker no usa el interceptor: hoy no cambia estados de envíos. Si en el futuro lo hace, hay que
  agregarlo también a su DbContext.
