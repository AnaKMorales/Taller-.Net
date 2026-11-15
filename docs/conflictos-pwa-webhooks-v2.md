# Conflictos de PWA y webhooks por versión

Implementación del 15/11/2026 para requisitos 32 y 36 del cambio de entrega parcial.

## Conflictos por bulto en la PWA

La API conserva su contrato: un 409 puede incluir `conflicto` con parada, versión actual
del envío, indicador de parada activa y lista de bultos (código, estado actual y motivo).
El cliente HTTP de la PWA lee ese objeto y lo adjunta al resultado rechazado. Solo acepta
el diagnóstico de la parada enviada y de los bultos de esa solicitud; una respuesta vieja
sin detalle, malformada o de otra parada mantiene el mensaje general de rechazo.

La cola conserva el diagnóstico, la solicitud original, su versión, identificador de
operación y evidencias en el almacén local del repartidor. Los registros anteriores, sin
la propiedad nueva, siguen siendo legibles. En Sincronización aparece una tabla con el
estado y motivo por bulto, y el enlace a la hoja para actualizarla.

El diagnóstico no autoriza modificar el resultado ya guardado por otro dispositivo:
no se aplica un subconjunto, no se cambia la versión original ni se reenvía automáticamente
una visita rechazada. El repartidor revisa la hoja actualizada y registra una nueva acción
solo cuando corresponde. Descartar sigue siendo una decisión explícita que elimina la
acción rechazada de la cola; no es necesario descartarla para consultar su diagnóstico.

### Ensayo manual de conflicto

Usar el mismo repartidor en dos dispositivos o perfiles de navegador, con una parada
activa descargada en ambos. Registrar visitas distintas sin conexión. Sincronizar la
primera y después la segunda: esta última queda rechazada y muestra los bultos y motivos.
Cerrar/reabrir la segunda PWA sin borrar los datos del sitio: conserva el diagnóstico y
las evidencias. Pulsar sincronizar otra vez no vuelve a enviar esa acción rechazada.
Actualizar la hoja desde su pantalla para revisar el estado confirmado.

La integración automatizada usa el cliente y la cola de la PWA con la API real, Bearer
y PostgreSQL, reemplazando IndexedDB por memoria. No sustituye la prueba en navegador.

## Webhooks V1 y V2

Cada suscripción tiene `VersionContrato`, que admite 1 o 2. Las existentes quedan en 1
al aplicar `20261115215128_VersionWebhookPorSuscripcion`. La base rechaza otros valores.
El portal `/portal/avisos` permite elegir el formato al crear una dirección y muestra
la versión en el listado. La versión es inmutable: para cambiarla, crear otra suscripción
y desactivar la anterior. No dejar ambas activas si el receptor no debe recibir ambas.

- **V1:** mismo JSON que recibían los receptores anteriores; no se agrega `bultos` ni
  se cambia su versión. Sigue siendo la selección predeterminada.
- **V2:** campos del contrato EventoEnvioV2 y resultados de bultos del intento original,
  con motivo y referencias a pruebas. No incluye archivos, fotos, nombres o datos de
  contacto del destinatario. Los eventos sin intento por bulto llevan `bultos: []` e
  `idOperacion: null`; no se inventan datos históricos que no existen.

El consumidor sigue aceptando eventos de outbox V1/V2 y utiliza una proyección interna
V1 compatible con los manejadores existentes. Al entregar V2 se consulta el intento
inmutable por envío e IdOperacion, con el mismo ámbito de operador/cuenta. No se usa
el estado actual del bulto para reconstruir su resultado histórico. Los intentos sin
HuellaSolicitud no se presentan como resultados por bulto.

Esto también resuelve el reenvío manual: puede seguir encolando el evento interno V1
con un MensajeId nuevo y recuperar exactamente sus resultados originales al entregarlo
a una suscripción V2. Los eventos administrativos de un envío parcial no reutilizan el
detalle de un intento anterior.

Ambos formatos pasan por el cliente HTTP existente, con HMAC del cuerpo exacto,
timestamp y X-Event-Id. No se cambian las reglas de destinos seguros, confirmación,
esperas o cola de fallidos. Una entrega exitosa no se vuelve a enviar al reintentar
otra fallida del mismo evento. Las notificaciones al destinatario no cambian.

### Migración y comprobación manual

Reiniciar Web con `scripts/Start-Web.ps1` aplica la migración sobre la conexión local
correcta; actualizar también la base y Worker del entorno desplegado antes de usar V2.
La implementación no aplica migraciones a desarrollo automáticamente durante las pruebas.

Con el **comercio del escenario actual**, abrir `/portal/avisos`, crear una dirección V2,
guardar el secreto mostrado una sola vez y configurar el receptor con él. Registrar un
intento parcial con el repartidor del mismo operador. Con outbox, RabbitMQ y Worker
ejecutándose, el receptor debe recibir `version: 2` y sus resultados en `bultos`.
Una dirección V1 debe seguir recibiendo su JSON anterior sin ese campo.

Para el receptor local, la configuración `Webhooks__PermitirDestinosPrivados=true` es
solo de desarrollo/pruebas. El receptor de `tools/UltimaMilla.ReceptorWebhooks` verifica
la firma y muestra las recepciones en `/recibidos`. No crear nuevos usuarios para repetir
el ensayo: usar suscripciones y visitas nuevas del escenario existente.

La prueba automatizada de recepción levanta un receptor HTTP local, verifica las firmas
de ambos formatos y simula dos HTTP 503 antes de confirmar V2. Comprueba igualdad con el
contrato original del outbox, reenvío manual, conservación tras modificar el envío,
aislamiento y ausencia de duplicados de las entregas ya exitosas. El recorrido completo
por RabbitMQ y la actualización de la PWA instalada requieren el ensayo final del equipo.
