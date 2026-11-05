# Demostración de webhooks firmados

Muestra, de punta a punta, los tres desenlaces posibles de un aviso a un comercio:

| Destino del receptor | Qué hace | Resultado esperado |
| --- | --- | --- |
| `/ok` | responde 200 | **exitoso** al primer intento (1 intento) |
| `/flaky?n=2` | falla (500) 2 veces por evento y después responde 200 | **con reintentos**: falla a los 0 s y 5 s, acierta a los 35 s (3 intentos) |
| `/fail` | responde 500 siempre | **termina en fallidos**: 0 s, 5 s, 35 s, 2 min 35 s (4 intentos) y pasa a `avisos.dlq` |

Además el receptor tiene `/slow?ms=10000` (tarda más que el timeout de 5 s del Worker, cuenta como fallo).
Todas las rutas verifican la firma `X-Signature` (HMAC-SHA256 de `"{X-Timestamp}.{cuerpo}"`) con su propio código
y responden 401 si falta o no coincide. `GET /recibidos` muestra cada llamada (hora, ruta, intento, firma, respuesta).

## Piezas

- `tools/UltimaMilla.ReceptorWebhooks`: el receptor de prueba (no es parte del producto, por eso está fuera de `src/`).
- `src/UltimaMilla.Worker/Dockerfile` y `tools/UltimaMilla.ReceptorWebhooks/Dockerfile`: imágenes del Worker y del receptor.
- `deploy/compose.yaml`: servicios `worker` y `receptor` junto a Postgres y RabbitMQ.
- `scripts/demo-webhooks.sh`: ejecuta los pasos de abajo y muestra el seguimiento.

## Pasos

1. **Infraestructura, Worker y receptor** (desde `deploy/`; hace falta `POSTGRES_PASSWORD` y `RABBITMQ_PASSWORD` en `deploy/.env`):

   ```bash
   docker compose --env-file .env up -d --build postgres rabbitmq worker receptor
   ```

   El Worker necesita la base ya migrada. Si la Web nunca corrió contra esa base: `dotnet ef database update --project src/UltimaMilla.Infrastructure`
   (con `ConnectionStrings__UltimaMilla` apuntando a ella). La contraseña de Postgres debe ser la misma con la que se inicializó
   el volumen (`deploy/.env` y `deploy/.env.postgres` deben coincidir; si no, el Worker no podrá autenticarse).

2. **La Web en Development** (`./scripts/Start-Web.ps1`, puerto 5243).
3. **El script**:

   ```bash
   ./scripts/demo-webhooks.sh        # WEB=http://127.0.0.1:5243 por defecto
   ```

   Crea un operador demo, entra como administrador, suscribe su cuenta a `http://receptor:8080/ok`, `/flaky?n=2` y `/fail`
   (solo al evento `Admitir`), genera un envío y sigue las llamadas durante unos 2 min 40 s. Salida real:

   ```
   HORA      RUTA     INTENTO  FIRMA   RESPUESTA
   21:34:07  /flaky   1        Valida  500
   21:34:07  /fail    1        Valida  500
   21:34:07  /ok      1        Valida  200      <- exitoso, no se vuelve a enviar
   21:34:12  /flaky   2        Valida  500      <- +5 s
   21:34:12  /fail    2        Valida  500
   21:34:42  /flaky   3        Valida  200      <- +30 s: el comercio "se recupera"
   21:34:42  /fail    3        Valida  500
   21:36:42  /fail    4        Valida  500      <- +2 min: se agotó, va a avisos.dlq
   ```

4. **Ver el resultado** en `http://127.0.0.1:5243/backoffice/fallidos` (usuario y clave los imprime el script):
   - *Cola de fallidos (avisos.dlq)*: el mensaje del alta, con botón **Reintentar**.
   - *Webhooks a comercios*: el destino `receptor` con 4 intentos.

   Para ver el reintento manual con éxito, cambiar `/fail` por una ruta que responda bien (o usar `/flaky?n=0`) y apretar **Reintentar**:
   el mensaje vuelve a `avisos` con el contador en cero y el Worker lo vuelve a procesar.

## Para mirar en vivo

```bash
docker compose --env-file .env logs -f worker receptor        # lo que hace cada uno
curl -s http://127.0.0.1:58080/recibidos | jq                  # historial del receptor (más reciente primero)
curl -s -X DELETE http://127.0.0.1:58080/recibidos             # reinicia historial y contadores de /flaky
```

## Notas

- El Worker bloquea por defecto los destinos en direcciones privadas (protección contra que un comercio apunte a la red
  interna). El receptor está en la red de Docker, así que `compose.yaml` activa `Webhooks__PermitirDestinosPrivados` en el
  Worker **solo para esta demo**.
- El secreto de la demo es `secreto-demo` (el mismo en las suscripciones y en `Receptor__Secreto`); cambiarlo con `WEBHOOK_SECRETO`.
- `datos-demo` deja eventos pendientes en el outbox y el Worker los publica apenas arranca; por eso el script espera unos
  segundos y suscribe solo a `Admitir`, para que la demostración muestre un único evento.
