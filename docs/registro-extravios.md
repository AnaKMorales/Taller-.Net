# Registro operativo de extravíos

Backend del requisito 11 de la letra original (5.2, pág. 4), implementado el 14/11/2026.
Reutiliza Envio.MarcarExtraviado y las transiciones declarativas existentes. No requiere
migración. El botón/formulario del backoffice queda a cargo de la responsable de Web.

## API

| Método | Dirección | Resultado |
| --- | --- | --- |
| GET | `/api/incidencias/envios/{envioId}/extravio` | Estado, código de seguimiento, bultos y versiones actuales de envío/ruta. |
| POST | `/api/incidencias/envios/{envioId}/extravio` | Registra el extravío de los bultos pendientes y devuelve las versiones confirmadas y el evento creado. |

Solo Administrador o Despachador autenticado y activo del operador. La API aplica rol,
aislamiento y no-store; el repositorio revalida identidad y rol en la base. Comercio,
Repartidor y OperarioDeposito no tienen este permiso. Una cuenta comercial dada de baja
no impide al personal autorizado gestionar la incidencia de un envío previo.
El POST exige el token de `/api/antiforgery` en `X-CSRF-TOKEN`, en la misma sesión.

```json
{
  "versionEsperadaEnvio": "<versionEnvio del GET>",
  "versionEsperadaRuta": "<versionRuta del GET o null si no tiene ruta>",
  "detalle": "No se encontró al verificar el vehículo"
}
```

El detalle es obligatorio, se recorta y admite hasta 2000 caracteres. No recibe estado,
responsable, operador o fecha desde el cliente: el responsable sale de la identidad
validada y la hora UTC del reloj del servidor. Se rechaza un momento anterior al último
evento persistido. Sin ruta, la versión esperada de ruta debe ser null; con ruta activa,
es obligatoria. Ambas versiones provienen de la consulta, no se generan en la interfaz.

La consulta devuelve `envioId`, `codigoSeguimiento`, `estado`, `versionEnvio`, `rutaId`,
`versionRuta` y `bultos` (id, código, estado). Puede consultarse un estado terminal, pero
eso no habilita la transición: Domain decide si el POST es válido.

El POST devuelve 200 con `envioId`, `estado`, `versionEnvio`, `eventoId`, `ocurridoEn`,
`rutaAfectadaId`, `versionRuta` y `bultos`. Ruta afectada es la que tenía la parada antes
de registrar el extravío, aun cuando la consulta posterior ya no muestre una ruta activa.
Sin ruta afectada ambos campos de ruta son null.

## Reglas, versiones y atomicidad

El método afecta todos los bultos pendientes de esta operación; no elige un subconjunto
de bultos por ID. Las reglas existentes admiten la incidencia desde depósito, asignado,
tránsito, no entregado, reprogramado y devolución; en un envío parcialmente entregado,
solo los pendientes se extravían y los entregados mantienen su resultado y evidencias.
Por ello, su resumen puede seguir siendo EntregadoParcialmente y no Extraviado.
Desde admitido, entregado, devuelto o ya extraviado no se inventan nuevas transiciones.

Si había parada activa se finaliza y se elimina la referencia activa del envío, conservando
la parada histórica. La ruta compite por su versión porque cambia una parada; no se
elimina la ruta ni se rinde automáticamente. No se cierra automáticamente una devolución
abierta, se cancela un aviso o se altera el dinero de una liquidación.

La lectura del grafo y versiones es consistente. Se comprueban las versiones del cliente
antes de decidir y las de EF al guardar: una asignación, entrega o cambio de ruta concurrente
puede rechazar la operación. Estado, bultos, parada, versiones, evento y mensaje outbox
se confirman o revierten juntos mediante el SaveChanges existente. El detalle queda en
el historial interno; el seguimiento público no lo expone. El outbox conserva su selección
actual de contratos V1/V2; este endpoint no modifica el formato externo de los avisos.

No se agrega idempotencia por identificador de operación: un doble envío con la misma
versión queda rechazado por conflicto después del primero. Ante pérdida de respuesta,
consultar el estado/historial antes de repetir. El estado persistido y el evento permiten
comprobar qué ocurrió; no se genera un segundo evento al aceptar una versión obsoleta.

## Errores e integración pendiente

400: identificadores/versiones/detalle inválidos o falta de versión de ruta/antiforgery.
401: sin sesión. 403: rol, actor o envío inaccesible (también un ID inexistente).
409: versiones cambiadas, estado no permitido o cronología incompatible. Fallo técnico:
500 genérico, sin dejar un registro parcial. Los errores técnicos quedan en logs existentes.

Web debe consultar versiones, pedir el detalle, confirmar la decisión y mostrar el resultado.
Un 409 requiere releer y reevaluar la incidencia; no se debe reenviar automáticamente
con versiones nuevas. Esta etapa no implementa esa pantalla ni cambia PWA o Worker.

Se verifican consulta sin efectos, permisos/CSRF, aislamiento/revocación, validación,
cronología, versiones de ambos agregados, entrega parcial, carrera entre decisiones,
historial/outbox único y rollback de todos los cambios ante fallo del mensaje.
