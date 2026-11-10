# ADR-007: incorporación de entrega parcial por bulto

Estado: en curso. Fecha: 01/11/2026. Requisitos 31–36 del cambio del 30/10.

## Contexto

El modelo inicial registraba un resultado completo por envío. El cambio para el equipo
de tres requiere resultados independientes por bulto, operación offline, seguimiento con
cantidades y avisos versionados. El servidor y sus contratos ya están disponibles para
las aplicaciones. El ensayo completo sigue pendiente.

## Decisiones implementadas

La autorización de entrega parcial y la acción sobre pendientes pertenecen a cada versión
de ReglasOperativas. Se reutilizan el backend administrativo, su aislamiento y su inmutabilidad.
Los envíos históricos conservan las versiones que referenciaban.

Cada bulto tiene un estado con transiciones declarativas. Envio sigue siendo la raíz que
valida y aplica las operaciones: no hay un cambio de estado público en Bulto. Para los
recorridos completos actuales se preparan primero todas las transiciones y se calcula el
resumen del envío desde sus resultados. Se conserva como columna persistida para no exigir
cargas de bultos a todas las consultas existentes. No es una columna calculada por PostgreSQL.

Un conjunto homogéneo refleja su estado equivalente. Con algunos entregados y otros no,
el resumen es EntregadoParcialmente, incluso con entregados más devueltos o extraviados.
Sin entregados se aplica la precedencia documentada en CAMBIOS-REQUERIMIENTOS.md.
El detalle por bulto será necesario para explicar situaciones que un resumen no expresa.
Esta precedencia puede revisarse al integrar flujos mixtos, antes de cerrar el ADR.

La migración infiere el estado actual de los bultos existentes desde su envío y operador:
el sistema previo solo admitía resultados completos. No crea intentos, fechas, eventos ni
evidencias que no existían. El nuevo estado del envío se agrega al final del enum para
conservar los valores anteriores.

## Reutilización y dificultades

El versionado de configuración, las FK de aislamiento y la concurrencia del agregado ya
admiten esta base: cambiar Bulto compite por la versión de Envio. No se necesita otra tabla
ni otro propietario para guardar el estado.

Intentos, evidencias, capacidad y asignación se diseñaron para el envío completo. El nuevo
resumen no basta: deben representar los bultos de cada visita, preservar resultados previos
y validar reenvíos y conflictos offline. No se agregan mutadores independientes que permitan
adelantar una entrega mixta sin esas validaciones.

Se implementó el registro por bulto con DetalleIntentoBulto y PruebaBulto. Validar la solicitud
completa antes de construir el grafo evita cambios parciales en memoria. Las FK se extendieron
para compartir EnvioId entre resultado, intento y bulto, e IntentoId entre evidencia y resultado:
el aislamiento por operador solo no evitaba cruces de dos envíos de la misma cuenta.
Los guards de pertenencia ahora identifican la columna del Id principal en claves de más
de dos componentes. Se mantuvieron sus comprobaciones de propietario real.

La repetición compara una huella del contenido con parada, responsable y asociaciones.
La parada de origen impide aplicar un intento offline antiguo a una visita nueva. No se
fusionan decisiones competidoras: se conserva la concurrencia del envío y el rollback.
El nuevo contrato de outbox V2 incluye resultados por bulto; la firma tradicional conserva V1.
El Worker consume ambos contratos y adapta V2 al formato V1 que reciben los comercios
actuales. Falta habilitar una salida V2 con el detalle por bulto para los receptores
que acepten ese contrato.

El 03/11 se conectó el registro con Application: una solicitud sin identidad en el body,
catálogo resuelto desde las reglas del envío y verificación de versión esperada. El repositorio
abre un contexto dedicado y carga el grafo con una lectura RepeatableRead, que termina antes
de ejecutar Domain. Una operación nueva con versión vieja se rechaza; un reenvío exacto se
reconoce sin guardar aunque lleve la versión original. El guardado conserva los tokens de EF
para detectar carreras posteriores a la lectura y no reintenta decisiones automáticamente.
La respuesta es un DTO y el contexto se dispone también al fallar. La PWA lo invoca en
`POST /api/repartidor/intentos` con la sesión del repartidor.

Se agregó BultoParada para conservar la composición y medidas de cada visita. La capacidad
de la ruta usa las medidas de paradas activas, no el peso total de envíos con bultos ya
entregados. Se conserva una sola asignación activa por envío y se eligen todos los pendientes
preparados; no se distribuye simultáneamente el mismo envío entre varias rutas.
Las transiciones del envío parcial mantienen el resumen mientras cambian solo sus pendientes;
el último entregado lleva el conjunto a Entregado. Se admiten visitas con o sin recargo fijo
y la devolución de pendientes según la versión de reglas.

El equipo eligió importe fijo por nueva visita. Se captura en la parada y se devenga al
despachar, evitando cargos por planificaciones retiradas antes de salir. RecargoVisita es
histórico y único por parada; no modifica la tarifa original ni representa un pago realizado.
El guardado atómico incluye cargo, estados, versiones y outbox. Devolucion.Bultos captura
solo pendientes y su cierre exige sus escaneos. Los entregados permanecen cerrados.
Las migraciones no fabrican recargos previos ni un importe para reglas antiguas con recargo;
esas reglas sin importe bloquean nuevas visitas y requieren una decisión explícita.
Ver [políticas e integración](../politicas-pendientes.md).

La migración inicializa las paradas anteriores con todos los bultos de su envío, como exigía
el modelo previo. Sus medidas se toman de los valores actuales porque no se registraba una
medición histórica por visita. Las asignaciones nuevas sí conservan su captura exacta.
Los eventos administrativos parciales usan V2 con IdOperacion nullable y sin resultados de
intento; los V2 de intento conservan su detalle. V1 no incorpora ese vocabulario nuevo.

La API de despacho permite preparar una ruta, asignarle una nueva visita con solo los
pendientes o iniciar su devolución. Reutiliza las reglas históricas y el cálculo de carga.
La asignación valida las versiones de envío y ruta y guarda la reprogramación y la nueva
parada juntas. Separar esos dos guardados podía dejar una reprogramación sin visita cuando
la ruta estuviera llena. El contrato está en [Rutas de pendientes](../rutas-pendientes.md).

La PWA descarga la composición de la parada y guarda en IndexedDB los resultados y
evidencias antes de enviarlos al endpoint autenticado de intentos. En cada reenvío conserva
la parada, la versión del envío y el identificador de operación originales.
Ver [ADR-005](005-sincronizacion-offline-repartidor.md).

## Conflictos al sincronizar resultados por bulto

Cada intento incluye todos los bultos en tránsito de la parada descargada. El resultado,
motivo y evidencias se validan por bulto. La visita se confirma como una sola operación,
porque cerrarla también cambia el estado del envío, la ruta y el evento del outbox.

| Caso | Comportamiento actual |
| --- | --- |
| Se pierde la respuesta y la PWA reenvía exactamente el mismo intento | Se reconoce el intento original y se devuelven sus resultados por bulto. |
| Dos solicitudes con el mismo identificador intentan guardar al mismo tiempo | Se compara la huella con la operación confirmada. Si coincide, se devuelve como repetida; si difiere, se rechaza. |
| Dos operaciones distintas llegan para los mismos bultos de una visita | La primera confirmada se conserva. La otra recibe un conflicto si pertenece al repartidor autorizado para esa parada. |
| La composición de una nueva visita incluye solo algunos bultos del envío | La operación antigua no se traslada a la nueva parada. El repartidor debe descargar la hoja y registrar los bultos que figuran en esa visita. |
| Faltan bultos de la parada, sobran bultos o hay evidencia inválida | Se rechaza la visita completa antes de modificar sus resultados. |

Se evaluó aplicar automáticamente solo los resultados que todavía parecieran válidos.
Esa alternativa puede dejar incompleta la evidencia de la visita y producir un cierre o
aviso distinto del que el repartidor registró. La versión del envío protege el cierre
conjunto. El servidor devuelve el diagnóstico por bulto de la composición autorizada,
tanto si la versión ya era obsoleta al leer el envío como si cambió durante el guardado.
En el segundo caso, se consulta el estado confirmado en un contexto nuevo. La PWA asocia
el conflicto HTTP con la visita de su cola y pide que se actualice la hoja. Queda pendiente
presentar ese detalle en la PWA.

## Pendientes para completar el cambio

- Conectar en backoffice las operaciones de asignación y devolución de pendientes.
- Mostrar en la PWA el detalle del conflicto que ya devuelve la API.
- Habilitar la salida V2 para los comercios compatibles y comprobar la recepción.
- Demostración offline/sincronización del 12/11 y evaluación final de dificultades y pruebas.

Las pruebas del núcleo y de migración no sustituyen esa demostración. SQL directo puede
evitar las reglas de Domain y debe tener sus propios controles.
