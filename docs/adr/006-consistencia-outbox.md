# ADR-006: consistencia entre cambios y mensajes mediante outbox

Estado: aceptada; implementados el registro transaccional y la coordinación del publicador.
Adaptador real del broker e integración con el host pendientes.

## Problema

Guardar una transición en PostgreSQL y publicar después en RabbitMQ son operaciones
separadas. Una falla entre ambas puede dejar el cambio confirmado sin su aviso.

## Decisión

Guardar un mensaje pendiente por cada EventoDeEnvio nuevo, dentro del mismo SaveChanges
y de la misma transacción que el cambio de negocio. La generación automática pertenece a
Infrastructure; el contrato de integración versionado pertenece a Application. Domain
conserva sus eventos históricos y permanece independiente del transporte y la persistencia.

El Id del mensaje coincide con el del evento y existe una restricción única por evento.
Se incluye el operador y la cuenta comercial en el contrato y se aplican los filtros y
restricciones existentes al registro persistente. No se reproducen eventos históricos
durante la migración.

## Consecuencias

Una falla al guardar o un conflicto de concurrencia revierte negocio y mensaje juntos.
No se necesita una transacción distribuida entre PostgreSQL y RabbitMQ. La publicación
será posterior y habrá consistencia eventual con los consumidores.

El componente PublicadorOutbox reserva una fila por transacción mediante FOR UPDATE SKIP LOCKED,
llama al contrato ITransporteMensajes y guarda PublicadoEn después de que ese contrato confirme.
Se usa un contexto dedicado por operador y se conserva el mismo identificador ante un reenvío.
Los bloqueos duran durante la confirmación: se acota la espera y no se garantiza orden estricto
con varios publicadores. El adaptador debe respetar cancelaciones y confirmar aceptación y ruta.

Esta decisión admite duplicados de transporte y requiere consumidores idempotentes. Un fallo
después de confirmar al broker y antes del commit puede volver a publicar el mismo mensaje.
Quedan pendientes adaptador RabbitMQ, ejecución periódica, reintentos/backoff, DLQ y observabilidad.
No se agrega Worker ni consumidor dentro de este componente de núcleo y datos.

Ver [alcance, integración y verificaciones](../outbox.md).
