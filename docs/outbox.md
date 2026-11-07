# Outbox transaccional y componente de publicación

## Alcance de esta etapa

Cada `EventoDeEnvio` nuevo guardado mediante `AppDbContext.SaveChanges` o
`SaveChangesAsync` genera automáticamente un `MensajeOutbox`. Incluye el alta y las
transiciones posteriores. Domain mantiene su historial y no depende de EF ni de RabbitMQ.

Los mensajes se agregan al mismo ChangeTracker antes del guardado. EF Core guarda los
cambios del negocio, los eventos y el outbox en su transacción habitual de PostgreSQL.
Si falla la inserción del mensaje o la comprobación de concurrencia, se revierte toda
la unidad de trabajo. Si el llamador abre una transacción explícita, debe confirmarla
para que persistan los cambios. No desactivar las transacciones automáticas de EF.

## Archivos y datos

- `Application/Mensajeria/EventoEnvioV1.cs`: contrato de integración independiente del
  transporte. Contiene los identificadores del mensaje, operador, cuenta, envío y evento,
  versión del contrato, tipo/origen, estados, fecha del evento e IdOperacion opcional.
- `Infrastructure/Persistence/Outbox/MensajeOutbox.cs`: registro técnico persistente.
  `Id` coincide con el identificador del evento y es estable; `Tipo` vale
  `envio.evento.v1` para la firma tradicional y `envio.evento.v2` para el registro por bulto;
  `Contenido` es una instantánea JSON; `CreadoEn` es la hora UTC del
  registro y `PublicadoEn` queda nulo mientras está pendiente.
- `OutboxPreparation`: detecta eventos Added y prepara una sola fila por evento. La
  cuenta se obtiene de la FK del envío, sin exigir cargar esa navegación.
- `OutboxModel`: mapea `MensajesOutbox`, su JSON como jsonb, la unicidad por evento y
  un índice parcial de pendientes ordenable por CreadoEn/Id. Este orden es de consulta;
  todavía no garantiza orden de publicación o procesamiento.
- `TenantSaveChangesInterceptor`: prepara los mensajes y luego valida los ámbitos y
  la concurrencia de toda la unidad de trabajo, incluidas las filas nuevas del outbox.

El JSON usa nombres camelCase y nombres de enumeraciones como texto. No incluye el
grafo de entidades, destinatario, dirección, evidencia ni token público. Es un aviso de
la transición, no una copia completa del envío. El consumidor debe interpretar la versión
1 para el formato tradicional. El método RegistrarIntentoPorBulto genera EventoEnvioV2
con versión 2 y resultados/motivos/Id de pruebas por bulto; no agrega contenido de archivos
ni datos personales. La firma anterior conserva V1. El Worker deberá enrutar/interpretar
las versiones y acordar el formato de los receptores; esto no implementa envío de webhooks.
Ver [contrato, repetición y límites](intentos-por-bulto.md).

Las transiciones administrativas de un envío parcial (reprogramación, asignación, despacho,
etc.) también van por V2, para conservar el vocabulario anterior de V1. IdOperacion en V2
es nullable: estos eventos llevan null y Bultos vacío, mientras los intentos llevan IdOperacion
y resultados de los bultos de su visita. No se reconstruyen resultados desde estados posteriores.

## Contrato para integrar el Worker

El publicador pasa al transporte el tipo, el identificador y el JSON persistidos.
El adaptador RabbitMQ usa el tipo como clave de enrutamiento y el identificador como
`message-id`. Los tipos y las versiones se exponen en los contratos de Application.

| Tipo del mensaje | Versión JSON | Contenido |
| --- | --- | --- |
| `envio.evento.v1` | 1 | Transición con el vocabulario tradicional, sin el campo `bultos`. |
| `envio.evento.v2` | 2 | Transición con resultados por bulto si corresponde a un intento. Las transiciones administrativas de un envío parcial llevan `bultos: []` e `idOperacion: null`. |

Cada evento tiene una sola fila de outbox. `mensajeId` y `eventoId` coinciden y se
conservan ante reenvíos. `idOperacion` identifica la visita registrada por el cliente.
`bultos` contiene los resultados de esa visita, incluso si el envío tuvo visitas previas.
`pruebas` contiene identificadores internos de evidencias. Una evidencia compartida
puede aparecer en varios bultos entregados. `motivoId` es nulo para un bulto entregado.

Ejemplo de evento interno de una entrega parcial:

```json
{
  "mensajeId": "10000000-0000-4000-8000-000000000001",
  "version": 2,
  "operadorId": "10000000-0000-4000-8000-000000000002",
  "cuentaComercialId": "10000000-0000-4000-8000-000000000003",
  "envioId": "10000000-0000-4000-8000-000000000004",
  "eventoId": "10000000-0000-4000-8000-000000000001",
  "tipoEvento": "EntregaParcial",
  "origen": "AppRepartidor",
  "estadoAnterior": "EnTransito",
  "estadoNuevo": "EntregadoParcialmente",
  "ocurridoEn": "2026-11-06T15:00:00Z",
  "idOperacion": "10000000-0000-4000-8000-000000000005",
  "bultos": [
    {
      "bultoId": "10000000-0000-4000-8000-000000000006",
      "resultado": "Entregado",
      "motivoId": null,
      "pruebas": ["10000000-0000-4000-8000-000000000009"]
    },
    {
      "bultoId": "10000000-0000-4000-8000-000000000007",
      "resultado": "Fallido",
      "motivoId": "10000000-0000-4000-8000-000000000008",
      "pruebas": []
    }
  ]
}
```

Este JSON es el contrato interno entre el productor y el Worker. La configuración de
RabbitMQ enlaza ambas versiones con `envio.#`. El consumidor actual acepta V1 y descarta
V2. Para completar la integración, el Worker debe reconocer V2, validar su ámbito e
identificador y elegir el formato de webhook compatible con el receptor. Esa selección
es independiente de la versión interna del evento.

## Aislamiento e integración

Las filas llevan `TenantId` y filtros por operador y cuenta. La cuenta se resuelve mediante
la relación MensajeOutbox → EventoDeEnvio → Envio. La FK compuesta al evento incluye
TenantId y evita referencias entre operadores. Sin tenant o desde seguimiento público no
se leen mensajes; las validaciones de escritura existentes también se aplican al outbox.

Los casos de uso continúan modificando Domain y guardando el mismo contexto; no tienen
que crear mensajes por separado. El alta existente en `CrearEnvioHandler` queda cubierta
mediante `AltaEnvioRepository`. Los cambios que no generan EventoDeEnvio, por ejemplo
editar un tarifario, todavía no producen mensajes de integración.

## Migración y límites

`TransactionalOutbox` agrega una tabla sin modificar datos de negocio. No genera mensajes
para eventos históricos: evita emitir avisos antiguos al actualizar la base. Aplicar con
`scripts/Test-LocalPersistence.ps1` o el procedimiento habitual de migraciones.

El registro transaccional y el componente que coordina la publicación están implementados.
El Worker usa el adaptador RabbitMQ para publicar en el broker. El consumo de V2 queda
pendiente de integrar.

El mecanismo cubre las escrituras a través de SaveChanges con eventos nuevos. SQL directo,
ExecuteUpdate/ExecuteDelete, cambios que evitan los métodos de Domain y modificaciones
de eventos históricos no generan automáticamente avisos. Después de un fallo de persistencia
se descarta el contexto y se vuelve a leer/revalidar según la política de concurrencia.

## Componente de publicación: alcance de núcleo y datos

`IPublicadorOutbox` es el contrato de Application para ejecutar un lote acotado. Su
implementación `PublicadorOutbox`, en Infrastructure, toma pendientes, llama al transporte
y guarda PublicadoEn después de la confirmación. No crea tareas en segundo plano ni
consume mensajes. No agrega endpoints, pantallas, contenedores ni dependencias de RabbitMQ.

`ITransporteMensajes` es el punto de integración para el adaptador de mensajería. Recibe
un `MensajeParaPublicar` con Id, operador, tipo, JSON y fecha de registro. La implementación
debe propagar MensajeId al broker, respetar el token de cancelación y completar
PublicarConfirmadoAsync únicamente tras una confirmación válida del broker y una ruta
de entrega válida. Un mensaje no enrutable, rechazo, timeout o conexión interrumpida no
se consideran éxito. No basta con entregar el mensaje al buffer de un cliente.

### Selección, confirmación y fallos

1. Se exige un AppDbContext dedicado y vacío, con TenantContext.ParaProcesoInterno para
   un solo operador. No se acepta un contexto de usuario, seguimiento o mensaje consumidor,
   ni una transacción previa o entidades del negocio ya cargadas.
2. Por cada mensaje se abre una transacción ReadCommitted y se selecciona un pendiente
   del operador con FOR UPDATE SKIP LOCKED. Los valores se parametrizan mediante EF.
3. El bloqueo de esa fila permanece durante la llamada al transporte y el marcado. Otra
   instancia puede tomar otra fila sin esperar ni seleccionar simultáneamente la misma.
4. Después de la confirmación, se asigna PublicadoEn con TimeProvider en UTC, se guarda
   con EF (incluidas sus validaciones de tenancy) y se confirma la transacción.
5. Ante un error o cancelación, se revierte esa transacción, se libera el bloqueo, se
   descarta el objeto rastreado y se propaga el error. El registro continúa pendiente.
   Los mensajes anteriores del lote que ya se confirmaron conservan su marcado.

El tamaño predeterminado del lote es 100, con valores admitidos de 1 a 1000. Se devuelve
el número confirmado en una ejecución satisfactoria. Si falla un mensaje, el lote se
interrumpe; el llamador debe consultar los pendientes para una nueva ejecución, sin
suponer que los mensajes anteriores también se revirtieron.

`OpcionesPublicacionOutbox.TiempoMaximoConfirmacion` fija la cancelación de la espera del
transporte (15 segundos por defecto, configurable hasta dos minutos). El adaptador debe
respetar esa cancelación. Una caída del proceso libera los bloqueos al cerrarse su conexión;
no se conserva una reserva permanente que pueda dejar el mensaje atascado.

La solución mantiene una transacción abierta durante cada publicación. Es una decisión
simple para este alcance; requiere confirmaciones acotadas y lotes pequeños. SKIP LOCKED
no garantiza orden estricto entre eventos cuando hay varias instancias publicando.

### Garantía y límites

Si el broker confirma y luego falla el marcado o el commit, el mensaje seguirá pendiente
y podrá enviarse nuevamente **con el mismo MensajeId**. El consumidor deberá deduplicarlo.
La garantía prevista al integrar transporte y ejecución periódica es al menos una vez,
no exactamente una vez. El publicador no implementa la idempotencia del consumidor.

Una falla detiene el lote: todavía no hay espera creciente, contador persistente de intentos,
aislamiento de mensajes problemáticos ni cola de fallidos. La política de nuevas ejecuciones
se integra desde el host. Tampoco se promete entrega al consumidor por el solo hecho de
marcar publicado: la confirmación requerida corresponde a la aceptación del broker.

### Cómo integrarlo sin mezclar responsabilidades

`AgregarPublicadorOutbox` registra IPublicadorOutbox, sus opciones y TimeProvider si todavía
no existe. El host debe registrar AppDbContext con su tenant interno e ITransporteMensajes.
No hay implementación ficticia predeterminada: sin transporte registrado, la resolución
del publicador falla en lugar de marcar mensajes sin enviarlos.

El componente, la coordinación de la tabla y sus pruebas pertenecen al apartado outbox.
Quedan para la integración de campo e infraestructura el adaptador RabbitMQ, el arranque
y planificación del proceso, Worker/consumidores, reintentos/backoff, DLQ y avisos firmados.
En esta etapa no se modifica Web, Redis, Docker Compose ni la infraestructura de despliegue.

## Verificación

Las pruebas de integración comprueban el alta con mensaje y contrato, guardado síncrono,
ausencia de duplicados al guardar sin cambios, rollback explícito, un error real de
restricción al insertar el mensaje, conflicto de concurrencia y aislamiento por operador,
cuenta y contexto público. Las pruebas de esquema verifican la migración actual.

Las pruebas del publicador usan PostgreSQL real y un transporte controlado **solo dentro
del proyecto de pruebas**: verifican confirmación antes del marcado, límites de lote,
aislamiento, dos publicadores concurrentes, liberación ante cancelación, fallo del transporte,
fallo real de PostgreSQL al marcar y reenvío con el mismo identificador. No verifican un broker
RabbitMQ real ni su configuración, porque el adaptador aún no está implementado.
