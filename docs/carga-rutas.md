# Registro de carga de rutas

Backend del requisito 19 de la letra original, integrado con el despacho del requisito 17.
Implementado el 13/11/2026. No requiere migración ni modifica pantallas o la cola offline.

## Contrato

Backoffice, con cookie y roles Administrador, Despachador u OperarioDeposito:

- `GET /api/carga/rutas/{rutaId}`
- `POST /api/carga/rutas/{rutaId}/bultos`

El POST exige token antiforgery de `/api/antiforgery`, enviado en `X-CSRF-TOKEN`.

PWA, con Bearer y política de repartidor:

- `GET /api/repartidor/carga/rutas/{rutaId}`
- `POST /api/repartidor/carga/rutas/{rutaId}/bultos`

El repartidor solo puede consultar/cargar su propia ruta. Se mantienen el CORS restringido
y la validación del usuario activo. Esta variante no usa cookies ni exige antiforgery.
No permite despachar: esa operación sigue reservada a administrador/despachador.

Ejemplo de cuerpo del POST:

```json
{ "codigo": "B001", "versionEsperada": "<GUID devuelto por GET>", "bultoId": null }
```

La consulta devuelve ruta, versión, instante de despacho, bultos esperados y su estado de
carga, más cantidades `esperados`, `cargados` y `faltantes`. Debe usarse el identificador
de una ruta planificada conocida; este avance no agrega un listado de rutas por cargar.

El código se compara sin distinguir mayúsculas, recortando espacios exteriores. Como
la unicidad del código es por envío, puede haber dos iguales en una ruta. En ese caso
el POST devuelve 409 y se debe indicar el `bultoId` de la consulta, manteniendo el código.
Un bulto ajeno o que no forma parte de una parada activa devuelve 400: no se registra una
carga sobrante ni se asigna automáticamente. Los faltantes se obtienen de la composición.

El resultado devuelve bulto, nueva versión, cantidades y `repetido`. Se deben usar las
versiones devueltas para las siguientes cargas y para despachar. No hay que cambiar el
identificador de ruta ni inventar versiones al recibir un conflicto.

## Repetición, estado y concurrencia

La carga usa `EscaneoBulto.RegistrarCarga`; el instante UTC lo toma el servidor. Se validan
la ruta abierta y la composición actual. La detección de carga comparte la misma lógica
que el despacho: escaneo de esa ruta, no futuro y no anterior a la última asignación.
Los bultos ya entregados de una visita previa no aparecen como faltantes de una nueva.

Un escaneo repetido de un bulto ya cargado en la visita actual devuelve 200 con
`repetido=true`, incluso con la versión anterior, sin guardar ni incrementar versiones.
No crea otro escaneo. Una ruta despachada/rendida rechaza los POST, también los reenvíos.
No es una clave idempotente global ni sustituye al identificador de operación de los
intentos offline; reconoce la carga por ruta, bulto y asignación actual.

Dos escrituras que partan de la misma versión compiten por la concurrencia optimista de
ruta y envío. Una puede confirmar y la otra recibe 409; al reenviar el mismo escaneo después
de ese conflicto se reconoce el ya persistido. Si eran bultos diferentes, se debe releer y
enviar la versión nueva. Un fallo de guardado revierte escaneo y versiones conjuntamente.
Los escaneos no generan un cambio de estado de envío ni un nuevo mensaje outbox; el
despacho posterior sí guarda sus eventos/outbox como antes.

## Límites de integración

Quedan a cargo de las interfaces la lectura de cámara/escáner, la selección de ruta y la
presentación de faltantes o códigos ambiguos. No se agregó un flujo offline de carga,
un registro persistido de sobrantes.
La [recepción con discrepancias](recepcion-deposito.md) ya tiene su propio backend.
La [asignación regular](asignacion-regular.md) también está disponible en backend.
El [cierre de devoluciones y rendición](cierre-deposito.md) dispone de sus propias APIs.
No se considera completo todo el circuito de depósito.

Las pruebas verifican cookie/CSRF y Bearer real, permisos del operario, repartidor asignado,
aislamiento, códigos ambiguos, reenvíos, carrera de cargas, rollback, carga de pendientes y
el recorrido hasta despacho y descarga de hoja.
