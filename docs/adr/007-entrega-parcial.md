# ADR-007: incorporación de entrega parcial por bulto

Estado: en curso. Fecha: 01/11/2026. Requisitos 31–36 del cambio del 30/10.

## Contexto

El modelo inicial registraba un resultado completo por envío. El cambio para el equipo
de tres requiere resultados independientes por bulto, operación offline, seguimiento con
cantidades y avisos versionados. Esta etapa prepara el núcleo; no describe una integración
completa ya terminada.

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
La negociación/enrutamiento de avisos para receptores existentes continúa pendiente en Worker.

El 03/11 se conectó el registro con Application: una solicitud sin identidad en el body,
catálogo resuelto desde las reglas del envío y verificación de versión esperada. El repositorio
abre un contexto dedicado y carga el grafo con una lectura RepeatableRead, que termina antes
de ejecutar Domain. Una operación nueva con versión vieja se rechaza; un reenvío exacto se
reconoce sin guardar aunque lleve la versión original. El guardado conserva los tokens de EF
para detectar carreras posteriores a la lectura y no reintenta decisiones automáticamente.
La respuesta es un DTO y el contexto se dispone también al fallar. No se agregó endpoint.

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

## Pendientes para completar el cambio

- Endpoint autenticado para el caso de uso RegistrarIntentoHandler ya implementado.
- API/hoja de ruta de la nueva asignación de pendientes e integración del despacho/devolución.
- Integrar los eventos V2 con el enrutamiento y compatibilidad de receptores existentes.
- Adaptaciones de Web, seguimiento, PWA y Worker por sus responsables.
- Demostración offline/sincronización del 12/11 y evaluación final de dificultades y pruebas.

Las pruebas del núcleo y de migración no sustituyen esa demostración. SQL directo puede
evitar las reglas de Domain y debe tener sus propios controles.
