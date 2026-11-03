# Registro de intentos por bulto

Avance de núcleo/datos del requisito 32 y soporte de los requisitos 31 y 36.
No incluye endpoints, PWA, sincronizador offline, Web ni consumidor Worker.

## Contrato del núcleo

```csharp
var intento = envio.RegistrarIntentoPorBulto(
    paradaId, resultados, repartidor, ocurridoEnUtc, idOperacion, posicion);
await db.SaveChangesAsync(cancellationToken);
```

El llamador debe cargar el envío completo con seguimiento: bultos, reglas con catálogo y
tipos de prueba, intentos con detalles/evidencias, parada y ruta/repartidor. El usuario,
operador y catálogo se resuelven en el servidor; no se aceptan entidades de Domain del cliente.
La orquestación de Application y el endpoint aún quedan por implementar: esto es el contrato
de Domain, no una API HTTP ya disponible.

Cada ResultadoBultoEntrega identifica un bulto, un resultado Entregado/Fallido, el motivo
de no entrega y su lista de PruebaEntrega. Deben informarse todos y solo los bultos de la
visita actual, una vez cada uno. En esta etapa la visita abarca todo el envío, antes de la
primera entrega parcial; una visita futura de solo pendientes corresponde al requisito 34.

El identificador de la parada se captura al preparar el trabajo offline. Una operación nueva
de otra parada se rechaza aunque el repartidor y el estado del envío coincidan. El núcleo
exige tránsito, el repartidor asignado, los límites de intentos/plazos y fechas UTC.

## Resultados y evidencias

La entrega de algunos bultos exige que la versión de reglas referenciada por el envío la
permita. No se usa la configuración más reciente para alterar un envío anterior.
Todos entregados producen Entregado, todos fallidos producen NoEntregado y una mezcla
produce EntregadoParcialmente. El intento tiene su resumen Entregado, Fallido o Parcial.

Cada entregado requiere todos los tipos de prueba configurados y no admite motivo de fallo.
Cada no entregado requiere un motivo activo de su catálogo; si exige evidencia, debe tenerla.
Las pruebas requieren posición y hora UTC. Una prueba puede compartirse entre varios bultos
entregados, pero no acreditar a la vez un entregado y un no entregado.
Las pruebas asociadas a un resultado fallido son evidencia de incidencia, no de entrega.

DetalleIntentoBulto conserva el resultado y motivo de cada bulto. PruebaBulto vincula ese
detalle con una prueba, permitiendo compartirla sin duplicar el archivo ni perder la asociación.
La llamada tradicional RegistrarIntento conserva su firma y produce detalles para todos los
bultos en los intentos nuevos. Su parámetro de resultado no admite Parcial: se usa el método nuevo.

Toda validación se realiza antes de vincular pruebas, crear registros o cambiar estados.
Al confirmar se libera la parada y la ruta puede rendirse; los pendientes quedan NoEntregado.
No se aplican aún la política de nueva visita/devolución, recargos o nueva asignación parcial.

## Repetición y conflictos

IdOperacion debe ser estable en un reenvío. Se guarda una huella SHA-256 del contenido:
parada, responsable, instante, posición, resultados, motivos y contenido/asociación de pruebas.
El orden de bultos y pruebas no importa. Los Id generados al reconstruir pruebas no forman
parte de la comparación; sí sus datos y referencias de archivo.

La misma operación con igual contenido devuelve el intento anterior, incluso si la parada
ya se liberó, sin agregar resultados, eventos ni outbox. Otro contenido con el mismo Id
se rechaza. Los intentos históricos sin huella no pueden reutilizarse por este método nuevo;
mantienen la repetición tradicional de la firma anterior.

Dos decisiones concurrentes no se fusionan: compiten por RowVersion de Envio y, al cerrar
la parada, también de Ruta. Las colisiones en índices de número/IdOperacion de intentos
o IdOperacion de eventos también se traducen a ConflictoConcurrenciaException, ya que
PostgreSQL puede detectarlas antes del UPDATE de la raíz. Se revierte todo y se descarta
el contexto perdedor. Después se relee para reconocer un reenvío o informar conflicto.
El endpoint futuro debe manejar esta respuesta y la versión esperada del trabajo offline;
este método no implementa una cola ni una sincronización automática.

## Persistencia y aislamiento

La migración 20261101201848_ResultadosEntregaPorBulto agrega dos tablas y HuellaSolicitud
nullable a IntentosEntrega. Las referencias compuestas incluyen:

- Resultado → intento y bulto, compartiendo EnvioId y TenantId.
- Asociación de prueba → resultado y prueba, compartiendo IntentoId y TenantId.

PostgreSQL impide cruzar un bulto de otro envío de la misma cuenta y operador, o una prueba
de otro intento. Índices únicos impiden repetir bulto por intento o prueba por detalle.
El CHECK obliga a que entregados no tengan motivo y fallidos sí lo tengan. Domain comprueba
además que el motivo sea activo y de la versión de reglas correcta.

Ambas entidades tienen filtros de operador/cuenta y validación de escrituras. Los padres
intermedios participan en la concurrencia del envío. SaveChanges protege como históricos
los intentos, pruebas, detalles y asociaciones persistidos; los hijos nuevos deben guardarse
con un intento nuevo. SQL directo puede evitar esa inmutabilidad de EF, pero no las FK/CHECK.

No se rellenan detalles o asociaciones para los intentos históricos: no conocemos qué
evidencia correspondía a cada bulto y no se inventa esa información.

## Eventos de integración

La firma tradicional mantiene envio.evento.v1. Los intentos del método nuevo generan
envio.evento.v2 con EventoEnvioV2: versión 2, el ámbito y la transición, más resultado,
motivo e identificadores de pruebas por bulto. El identificador del mensaje sigue siendo
el del evento. No se incluyen datos personales, archivos ni tokens públicos.

Estado, intento, detalles, asociaciones, evento y mensaje se guardan en la misma transacción.
Un fallo del outbox o conflicto revierte el conjunto. No cambia el transporte abstracto.
El Worker deberá interpretar/enrutar versiones y negociar el contrato de los webhooks
existentes; no se implementó esa parte ni se entrega V2 a receptores antiguos automáticamente.
