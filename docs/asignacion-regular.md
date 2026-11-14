# Asignación regular de envíos a rutas

Backend de la letra original, sección 5.3, requisitos 13–16. Implementado el 14/11/2026.
Reutiliza la creación de rutas y las restricciones existentes del dominio y de PostgreSQL.
No requiere migración ni cambia los contratos de la PWA, los avisos o las visitas parciales.

## API para el backoffice

Requiere administrador o despachador autenticado. El operador y el responsable se obtienen
de la sesión y se revalidan en la base. Depósito, comercio y repartidor no asignan rutas.
Los POST requieren el token de `/api/antiforgery` en `X-CSRF-TOKEN`, conservando las cookies.

| Operación | Dirección | Resultado |
| --- | --- | --- |
| Envíos candidatos | GET `/api/despacho/envios/disponibles` | Envíos EnDeposito o Reprogramado, sin otra parada activa ni devolución, con versión, cantidad de bultos, peso, volumen y ventana UTC. |
| Rutas abiertas | GET `/api/despacho/rutas/planificables` | Rutas sin despacho ni rendición, con repartidor activo, versión, carga actual, capacidades, máximo de paradas y próximo orden sugerido. |
| Flota del operador | GET `/api/despacho/recursos` | Repartidores activos con sus identificadores y vehículos con sus identificadores y capacidades. |
| Crear ruta | POST `/api/despacho/rutas` | Operación existente: recibe repartidor, vehículo y máximo de paradas; crea una ruta vacía. |
| Asignar envío | POST `/api/despacho/rutas/{rutaId}/envios` | Crea la parada y devuelve sus bultos, medidas y versiones nuevas del envío y de la ruta. |

Crear una ruta sigue usando el contrato existente `CrearRutaPendientes`, cuyo nombre se
conserva por compatibilidad. La operación ya permitía crear rutas vacías del operador,
sin exigir una entrega parcial:

```json
{
  "repartidorId": "<GUID de recursos>",
  "vehiculoId": "<GUID de recursos>",
  "maxParadas": 10
}
```

Asignar:

```json
{
  "envioId": "<GUID de envíos disponibles>",
  "versionEsperadaEnvio": "<versión del envío>",
  "versionEsperadaRuta": "<versión de la ruta>",
  "orden": 1,
  "llegadaEstimada": "2026-11-15T14:00:00-03:00"
}
```

La asignación devuelve 201 con `rutaId`, `envioId`, `paradaId`, `versionEnvio`,
`versionRuta`, `orden`, `llegadaEstimada` UTC, `bultos`, `pesoKg` y `volumenM3`.
Las versiones deben actualizarse después de cada asignación y antes de otra operación.
Las consultas devuelven 200 y no se almacenan en caché. Un listado vacío significa que
no hay candidatos visibles en ese ámbito; no garantiza que se hayan cargado datos demo.

Errores: 400 campos incompletos/orden inválido o antiforgery; 401 sin sesión;
403 rol, identidad, envío, ruta o flota ajenos/inaccesibles; 409 versión vieja o condición
operativa incompatible. Un reenvío después de confirmar la asignación devuelve 409,
sin crear otra parada ni evento: no se agregó una clave global de idempotencia.

## Validaciones y criterio de orden

La operación regular acepta envíos completos preparados EnDeposito o Reprogramado.
No modifica su programación ni su tarifa y no prepara automáticamente un reintento.
Los envíos EntregadoParcialmente siguen usando `/pendientes`, que conserva la selección
de bultos y las políticas de nueva visita/recargo ya implementadas.

La consulta de rutas muestra también las abiertas sin capacidad libre; sus cantidades
permiten al backoffice informar la ocupación. Estar en el listado no garantiza que una
asignación concreta sea válida. El POST vuelve a comprobar todos los datos:

- Estado y versión del envío y de la ruta, y repartidor activo con el rol correcto.
- Ruta sin despachar ni rendir; envío sin otra asignación ni devolución.
- Orden positivo y no repetido en la ruta, incluyendo sus paradas históricas.
- Número máximo de paradas activas y capacidad de peso/volumen, sumando la composición
  conservada de las otras paradas activas y los bultos del nuevo envío.
- Llegada no anterior a la asignación y dentro de la ventana comprometida: inicio
  incluido y fin excluido. Si hubo intentos, se respeta también su plazo entre visitas.

El criterio de orden es manual: el despachador fija `orden` y la llegada estimada según
la distribución geográfica y las ventanas que consulta. La hoja se descarga en ese
orden. `proximoOrden` sugiere el máximo histórico más uno; se pueden usar otros números
positivos libres. No hay optimización geográfica ni cálculo de tiempos entre paradas.
Los candidatos se muestran por inicio de ventana UTC e identificador como desempate.

## Consistencia

`AsignarEnvioRutaHandler` en Application valida el comando, las dos versiones y el ámbito
regular y llama a `Ruta.AgregarParada`. El repositorio de Infrastructure carga envío y ruta
con seguimiento, sus composiciones y relaciones necesarias en una lectura Repeatable Read.
Cierra esa lectura y conserva las versiones originales para el guardado optimista.

Un SaveChanges guarda la parada, su composición, los estados de bultos/envío, el evento
Asignar o Reasignar y su mensaje de outbox. Si falla cualquier escritura se revierte todo.
Una operación concurrente sobre el mismo envío o ruta produce conflicto y exige releer,
sin mezclar decisiones. El índice de asignación activa impide duplicar el envío en dos
rutas. El índice de orden de parada también se traduce a conflicto de concurrencia si
dos despachadores eligen simultáneamente el mismo número.

Tras asignar se usan las APIs existentes de [carga](carga-rutas.md) y
[despacho](despacho-operativo.md); después la PWA puede descargar la hoja.

## Alcance y pruebas

Falta conectar las consultas y el POST a los formularios del backoffice. Este avance
no agrega pantallas ni cambia la PWA. Tampoco incorpora administración de flota,
retiro/reordenamiento de paradas ya asignadas ni reprogramación interna nueva.
El [cierre de devoluciones y rendición](cierre-deposito.md) tiene sus propias APIs.

Pruebas unitarias y con PostgreSQL cubren capacidades, ventanas, orden, versiones, roles,
aislamiento, CSRF, conservación de programación/tarifa, asignación de reprogramados y
exclusión de parciales. Se verifica el recorrido desde recepción hasta descarga de hoja,
el rollback por fallo del outbox y carreras por el mismo envío, la capacidad de una ruta
y el mismo número de parada.
