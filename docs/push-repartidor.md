# Aviso al repartidor con la app cerrada (Web Push)

Reentrega: **una reasignación hecha desde el Backoffice llega al dispositivo con la app cerrada.**

Se usa **Web Push** estándar (Push API + RFC 8030/8291/8292, claves VAPID). No hace falta una app nativa ni Firebase:
el navegador del celular (Chrome, Edge, Firefox; Safari en iOS 16.4+ con la PWA instalada en la pantalla de inicio)
mantiene la conexión con su servicio push y despierta al **service worker** de la PWA, que muestra la notificación
aunque la app esté cerrada.

## Recorrido

```
Backoffice (Depósito → asignar envío a ruta)
  └─ Envio: evento Asignar / Reasignar ──(outbox, misma transacción)──► RabbitMQ "avisos"
                                                                         └─ Worker: AvisoPushRepartidor
                                                                              ├─ parada activa → ruta → repartidor
                                                                              ├─ SuscripcionesPush del repartidor
                                                                              └─ POST cifrado + firma VAPID ──► servicio push
                                                                                                                 (FCM, Mozilla, Apple)
Celular (app cerrada) ◄── el servicio push despierta al service worker ── showNotification("Envío reasignado a tu ruta")
  └─ tocar la notificación abre la PWA en la hoja de ruta
```

- **Suscripción** (PWA → Web): en la hoja de ruta, **Activar avisos** pide permiso de notificaciones, se suscribe con la
  clave pública VAPID (`GET /api/repartidor/avisos/clave`) y registra el dispositivo (`POST /api/repartidor/avisos/suscripcion`).
  Repartidor y operador salen del token, nunca del cuerpo. Al **Salir** se da de baja (`POST .../suscripcion/baja`).
- **Tabla** `SuscripcionesPush` (Infrastructure, registro técnico con `TenantId` y FK al repartidor del mismo operador).
- **Envío** (Worker): `AvisoPushRepartidor` es otro `IManejadorEventoEnvio` del compuesto (destinatario → repartidor →
  webhooks). Solo reacciona a `Asignar` y `Reasignar`. Idempotencia propia `(MensajeId, "push-repartidor")`.
- **Contenido**: título, «Envío ABCD1234 · parada N», hora estimada (la formatea el celular en su hora local). Va
  cifrado de punta a punta (`CifradoWebPush`, RFC 8291 `aes128gcm`, verificado con el vector oficial del RFC): el servicio
  push no lo puede leer. La etiqueta es por envío: si llega repetido, el celular muestra una sola notificación.

## Fallos

| Respuesta del servicio push | Qué pasa |
| --- | --- |
| 201 | Entregado. |
| 404 / 410 | El dispositivo se dio de baja: se borra la suscripción, sin reintento. |
| 429 / 5xx / sin conexión / timeout (5 s) | Excepción: el mensaje sigue por `avisos.espera.5s/30s/2m` y, si no, a `avisos.dlq`. |
| 400 / 403 / 413 | Se registra en el log; reintentar no lo arregla. |

Si el celular está apagado o sin señal, el servicio push guarda el aviso hasta 12 h (`TTL`).

**Seguridad.** El endpoint lo manda el dispositivo: el Worker solo envía a servicios push conocidos (`fcm.googleapis.com`,
`updates.push.services.mozilla.com`, `*.push.apple.com`, `*.notify.windows.com`…), por HTTPS en el puerto 443, sin
redirecciones ni proxy y conectando solo a IPs públicas (mismo `ConnectCallback` que los webhooks). Lo mismo se valida al
registrar la suscripción.

## Configuración

| Variable | Dónde | Secreta |
| --- | --- | --- |
| `WEBPUSH_CLAVE_PUBLICA` → `WebPush__ClavePublica` | Web y Worker | No |
| `WEBPUSH_CLAVE_PRIVADA` → `WebPush__ClavePrivada` | Solo Worker | **Sí** |
| `WEBPUSH_ASUNTO` → `WebPush__Asunto` | Worker | No (`mailto:` de contacto) |

Sin claves, Web responde 404 en `/avisos/clave` (la PWA lo informa) y el Worker no envía nada. El Worker valida al
arrancar que la privada corresponda a la pública.

```bash
scripts/generar-claves-webpush.sh >> deploy/.env      # local (deploy/.env no se versiona)
```

En AWS, cargar las dos claves en SSM (las baja `desplegar.sh` al `.env`; `<ruta>` es la salida `ruta_parametros_ssm`):

```bash
scripts/generar-claves-webpush.sh | while IFS='=' read -r nombre valor; do
  tipo=String; [ "$nombre" = WEBPUSH_CLAVE_PRIVADA ] && tipo=SecureString
  aws ssm put-parameter --name "<ruta>/$nombre" --type "$tipo" --value "$valor" --overwrite
done
```

Generar las claves **una sola vez**: si cambian, las suscripciones existentes dejan de servir y cada repartidor tiene que
volver a abrir la app (se re-suscribe sola al detectar la clave nueva).

## Demostración

1. Claves en `deploy/.env` y `docker compose --profile app up -d` (aplica la migración `SuscripcionesPushRepartidor`).
2. En el celular (o Chrome de escritorio) abrir `https://<dominio>/repartidor/`, ingresar como repartidor y tocar
   **Activar avisos** → permitir. Debe decir «Avisos activados». En local alcanza con `http://localhost:8000/repartidor/`
   (localhost es contexto seguro); desde un celular hace falta HTTPS (el despliegue en AWS).
3. **Cerrar la app** (deslizarla fuera de las recientes).
4. En el Backoffice, **Depósito** → asignar un envío (o uno reprogramado, que es la reasignación) a una ruta de ese repartidor.
5. En segundos llega «Envío reasignado a tu ruta · Envío XXXX · parada N». Tocarla abre la hoja de ruta.
   En el log del Worker: `Aviso push Reasignar del envío … : 1 de 1 dispositivos`.

Límites: iOS solo recibe push con la PWA instalada en la pantalla de inicio; si el usuario bloquea las notificaciones la
PWA lo muestra y no puede volver a pedir permiso (se habilita en los ajustes del navegador). El modo ahorro de batería de
algunos Android puede demorar el aviso.
