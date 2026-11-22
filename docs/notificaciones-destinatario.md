# Aviso al destinatario

Letra, requisito 27: "Notificación automática al destinatario ante los cambios de estado relevantes"; requisito 5:
la identidad visual del operador se refleja también "en las comunicaciones al destinatario".

## Cómo funciona

1. Cada cambio de estado ya genera un `EventoDeEnvio` y, por el outbox, un mensaje en la cola `avisos`.
2. En el Worker, `ProcesadorAvisos` resuelve el tenant del mensaje y llama al manejador del evento, que ahora es un
   compuesto (`ManejadoresEventoEnvio`): primero `NotificacionDestinatario` y después `EntregaWebhooks`.
3. `NotificacionDestinatario` (Infrastructure/Notificaciones):
   - Solo actúa en estados relevantes y si el estado realmente cambió: Admitido, En tránsito, Entregado, Entregado
     parcialmente, No entregado, Reprogramado, En devolución y Extraviado. Los internos (en depósito, asignado a
     ruta) y el cierre de la devolución (Devuelto, que importa al comercio) no se avisan.
   - Arma el texto con `MensajesDestinatario` (Application, puro): marca y contacto del operador, nombre del
     destinatario, ventana de entrega en hora local y enlace `/seguimiento/{código}`.
   - Lo envía por el puerto `IEnviadorNotificaciones` y guarda una `Notificacion` (tabla existente `Notificaciones`)
     ya marcada como enviada. No hubo migraciones.
4. En el backoffice, el detalle del envío muestra la sección **Avisos al destinatario**.

## Garantías

- **Idempotencia propia**, con la clave (MensajeId, `notificaciones`) en `MensajesProcesados`, separada de la de
  los webhooks (`avisos`). Si los webhooks fallan y el mensaje vuelve por la cola de espera, el destinatario no
  recibe el aviso de nuevo. Lo prueba `AvisoAlDestinatarioNoSeDuplicaCuandoFallanLosWebhooksYElMensajeSeReintenta`.
- **Si el envío falla**, no se guarda nada, la reserva se deshace y el mensaje se reintenta (5 s → 30 s → 2 min → DLQ,
  como los webhooks). Los demás manejadores corren igual.
- **Al menos una vez:** si el envío salió pero falló el guardado posterior, el reintento lo reenvía. No hay
  "exactamente una vez" con un proveedor externo.
- **Aislamiento:** corre con el `TenantContext` del mensaje; con otro operador ni siquiera se puede reservar el
  mensaje y el envío no se lee.

## Configuración

- `Notificaciones__UrlPublica` (Worker): base del enlace, por ejemplo `https://<dominio>`. Sin ella el aviso lleva la
  ruta relativa `/seguimiento/{código}`. Ya está configurada en `deploy/`: en `compose.yaml` toma `URL_PUBLICA`
  (por defecto `http://localhost:8000`) y en `compose.aws.yaml` usa `https://${DOMINIO}`.

## Correo (SMTP)

El Worker elige el enviador al arrancar:

- **Con `Smtp__Servidor`**: `EnviadorNotificacionesSmtp` (MailKit) manda un correo de texto plano al email del
  destinatario. Abre una conexión por aviso y, si falla (servidor caído, credenciales malas, timeout), lanza el error
  para que el mensaje se reintente por la cola. Si falta el remitente o el usuario no tiene clave, el Worker no arranca.
- **Sin `Smtp__Servidor`**: `EnviadorNotificacionesRegistro` deja el aviso solo en el log (destino enmascarado).

| Variable (Worker) | En `deploy/.env` / SSM | Defecto en compose |
| --- | --- | --- |
| `Smtp__Servidor` | `SMTP_SERVIDOR` | `mailpit` (vacío = solo log) |
| `Smtp__Puerto` | `SMTP_PUERTO` | `1025` |
| `Smtp__Seguridad` | `SMTP_SEGURIDAD` (`StartTls`, `SslAlConectar`, `Ninguna`) | `Ninguna` |
| `Smtp__Usuario` / `Smtp__Clave` | `SMTP_USUARIO` / `SMTP_CLAVE` | vacíos (sin autenticación) |
| `Smtp__Remitente` / `Smtp__NombreRemitente` | `SMTP_REMITENTE` / `SMTP_NOMBRE_REMITENTE` | `avisos@ultimamilla.local` / `UltimaMilla` |

**Local:** con `--profile app` los avisos van a **Mailpit**, un SMTP de prueba que no los manda a nadie y los muestra
en `http://127.0.0.1:58025`.

**Que lleguen de verdad** (por ejemplo con Gmail y una clave de aplicación), en `deploy/.env`:

```
SMTP_SERVIDOR=smtp.gmail.com
SMTP_PUERTO=587
SMTP_SEGURIDAD=StartTls
SMTP_USUARIO=cuenta@gmail.com
SMTP_CLAVE=<clave de aplicación>
SMTP_REMITENTE=cuenta@gmail.com
```

**AWS:** los mismos nombres como parámetros bajo la ruta de SSM del ambiente (`SMTP_CLAVE` como SecureString);
`desplegar.sh` los baja al `.env` en el próximo despliegue. Sin ellos, los avisos quedan en Mailpit dentro de la EC2.
La clave no está en Terraform para que no quede en el estado:

```
aws ssm put-parameter --name <ruta>/SMTP_CLAVE --type SecureString --value '<clave>'
aws ssm put-parameter --name <ruta>/SMTP_SERVIDOR --type String --value smtp.gmail.com
```

## Límites

- **El correo sale por SMTP solo si se configura** (ver arriba). Sin servidor, el aviso queda en el log y se da por
  enviado. SMS o WhatsApp necesitarían otra implementación de `IEnviadorNotificaciones`; el enviador SMTP los rechaza.
- El remitente es uno solo para todos los operadores (`Smtp__Remitente`); la marca del operador va en el texto.
- Gmail tiene un límite diario de envíos; para más volumen conviene SES u otro proveedor por SMTP.
- Un solo canal (Email, al email del destinatario). `CanalNotificacion` ya prevé SMS y WhatsApp.
- El texto está en el código (`MensajesDestinatario`), no es configurable por operador; la marca y el contacto sí
  salen de la identidad visual de cada operador.
- El enviador SMTP se probó contra un servidor SMTP falso en los tests (`CorreoSmtpTests`). [NO VERIFICADO] el envío
  contra Gmail o SES real y contra Mailpit en compose.
- [NO VERIFICADO] el aviso de punta a punta con RabbitMQ real (Web → outbox → cola → Worker): se probó el manejador
  contra PostgreSQL y el compuesto con los webhooks; el tramo de la cola es el mismo que ya usan los webhooks.
