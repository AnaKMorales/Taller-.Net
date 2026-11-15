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
  ruta relativa `/seguimiento/{código}`. En AWS todavía no está configurada (requiere un cambio en `deploy/`).

## Límites

- **No hay un proveedor de correo real.** `EnviadorNotificacionesRegistro` deja el aviso en el log (asunto y destino
  enmascarado, `p***@dominio`) y lo da por enviado. Un proveedor (SMTP, SES, SMS) se enchufa implementando
  `IEnviadorNotificaciones`; necesita credenciales y configuración de despliegue que no se agregaron.
- Un solo canal (Email, al email del destinatario). `CanalNotificacion` ya prevé SMS y WhatsApp.
- El texto está en el código (`MensajesDestinatario`), no es configurable por operador; la marca y el contacto sí
  salen de la identidad visual de cada operador.
- [NO VERIFICADO] el aviso de punta a punta con RabbitMQ real (Web → outbox → cola → Worker): se probó el manejador
  contra PostgreSQL y el compuesto con los webhooks; el tramo de la cola es el mismo que ya usan los webhooks.
