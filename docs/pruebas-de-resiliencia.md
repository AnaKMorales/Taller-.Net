# Pruebas de resiliencia

Requerimiento opcional "Pruebas de resiliencia" (sección 7.4, 2 puntos): interrumpir deliberadamente dependencias
(caché, cola, base de datos, servicio externo) y documentar el comportamiento degradado esperado y el observado.
Responsable: Aldana Rodríguez. Script: `scripts/probar-resiliencia.sh`.

## Cómo se ejecuta

Con el stack completo de `deploy/compose.yaml` levantado (perfil `app`, `WEB_ENTORNO=Development` para usar
`/dev/datos-demo` y `/dev/simular`, y `WEBHOOKS_PERMITIR_DESTINOS_PRIVADOS=true` porque el receptor es privado):

```bash
docker compose --env-file deploy/.env -f deploy/compose.yaml --profile app up -d
./scripts/probar-resiliencia.sh          # los 5 escenarios, ~6 min
./scripts/probar-resiliencia.sh 2 4      # solo algunos
```

El script crea un operador demo, entra como administrador, suscribe su cuenta al receptor de prueba
(`/ok`, `/flaky`, `/fail`) y genera altas con `/dev/simular/alta`. Corta cada dependencia con `docker compose stop`,
mide con `curl` y con consultas a la base, la vuelve a levantar y compara contra lo esperado. Al salir (incluso con
Ctrl+C) vuelve a levantar todo lo que detuvo. Deja el informe en `TestResults/resiliencia-<fecha>.md` y termina con
código 1 si algún escenario no se comportó como se esperaba.

Cómo mide: el avance del outbox es `MensajesOutbox.PublicadoEn IS NULL`; la entrega de avisos se cuenta en
`EntregasAviso` (filas exitosas de la suscripción `/ok` de la cuenta de prueba), no en el historial del receptor, que
vive en memoria y se pierde al reiniciarlo. Los eventos repetidos se buscan en el receptor (mismo `X-Event-Id` con 200).

## Resultados (corrida local del 15/11/2026)

| Dependencia cortada | Comportamiento degradado esperado | Observado | |
|---|---|---|---|
| **Redis** (caché) | Seguimiento y tarifa siguen respondiendo desde la base. Solo la primera llamada paga el timeout de Redis; después `CacheSegura` deja de intentar 30 s. `/ready` no depende de Redis. | Seguimiento 200 siempre: 1.ª llamada 5,6 s, siguientes 0,015 s y 0,012 s. `/ready` 200. Al volver Redis se usa otra vez la caché. | ✅ |
| **RabbitMQ** (cola) | La Web sigue aceptando altas: el evento queda en el outbox en la misma transacción. Al volver la cola se publica y llega el aviso, sin perder ni duplicar. | Altas 204. 2 mensajes pendientes en el outbox. Publicados 17 s después de volver RabbitMQ; avisos +2. | ✅ (tras la corrección de abajo) |
| **PostgreSQL** (base) | `/live` 200 (el proceso vive) y `/ready` 503 (avisa que no puede atender, para un healthcheck o balanceador). Escrituras rechazadas sin estado a medias. Web y Worker se reconectan solos. | `/live` 200, `/ready` 503, seguimiento en caché 200, alta 500. `/ready` 200 1 s después de volver; alta 204; el Worker publica el outbox en 2 s. | ✅ |
| **Receptor del comercio** (servicio externo) | El intento fallido queda en `EntregasAviso`; el mensaje pasa por `avisos.espera.5s → 30s → 2m` y llega en un reintento, sin intervención. | Caído: `false/1` (no entregado, 1 intento). Al volver: `true/3`, entregado en el reintento de +35 s. | ✅ |
| **Worker** (consumidor) | La Web no se entera (se comunican solo por la cola). Los eventos esperan; al volver se procesan exactamente una vez. | Altas 204; 3 pendientes. Publicados 4 s después de volver; avisos +3; 0 repetidos. | ✅ |

El escenario del receptor es el escenario 6 de la defensa (sección 9.3): detener el receptor, generar eventos,
reactivarlo y verificar la recuperación.

## Fallo encontrado y corregido

La primera corrida del escenario de RabbitMQ falló: el outbox se publicaba al volver la cola, pero **no llegaba
ningún aviso**. `rabbitmqctl list_queues` mostraba `avisos` con 21 mensajes y **0 consumidores**, hasta reiniciar
el Worker a mano.

Causa: `ConexionRabbitMq.ObtenerAsync` descartaba la conexión cuando la encontraba cerrada y abría otra. Mientras
RabbitMQ estaba caído, el publicador del outbox (que corre cada ciclo) pedía la conexión, la encontraba cerrada y la
reemplazaba. Pero esa conexión tenía `AutomaticRecoveryEnabled` y se estaba recuperando sola junto con sus canales,
incluido el del consumidor de `avisos`. Al descartarla, el consumidor quedaba colgado de una conexión muerta.
Si no había nada pendiente en el outbox durante la caída, el publicador no pedía la conexión y la recuperación
funcionaba, por eso el fallo no aparecía siempre.

Corrección (`src/UltimaMilla.Infrastructure/Mensajeria/ConexionRabbitMq.cs`): si la conexión no la cerró la propia
aplicación, no se reemplaza; se lanza una excepción y el publicador reintenta en el próximo ciclo (su `catch` ya
lo contemplaba). La conexión se recupera sola y con ella el consumidor. Después de la corrección, los 5 escenarios
pasan y el consumidor vuelve unos 10 s después de levantar RabbitMQ.

## Limitaciones

- Se corre en local, sobre el mismo `compose.yaml` que usa la EC2 (en AWS se suma `compose.aws.yaml`, que solo cambia
  puertos y TLS). No hay una corrida automática en el pipeline: cortar contenedores a mitad de las pruebas haría
  inestable el CI.
- La caché del seguimiento dura pocos segundos (los datos cambian seguido). Con la base caída, el seguimiento solo
  responde si la consulta todavía está en caché; si no, responde 500. Es lo esperado: la caché no reemplaza a la base.
- Redis caído cuesta un timeout de unos 5 s cada 30 s (al terminar la pausa de `CacheSegura` se vuelve a probar).
- Con la base caída, el alta responde 500 genérico. No se agregó un 503 específico.
- No se probó la caída de Traefik (es el único punto de entrada; los equipos de tres están eximidos del balanceo) ni
  la pérdida de los volúmenes.
