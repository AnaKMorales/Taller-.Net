# Recepción en depósito con discrepancias

Backend del requisito 10 de la letra original, sección 5.2. Implementado el 14/11/2026.
Reutiliza las entidades y tablas existentes; no requiere migración.

## Contrato para integrar al backoffice

Requiere sesión de administrador, despachador u operario de depósito del operador.
Los POST usan el token de `/api/antiforgery` en `X-CSRF-TOKEN`, conservando las cookies.
No recibe operador ni usuario responsable en el cuerpo: se resuelven desde la sesión
y se comprueba en la base que el usuario siga activo y tenga el rol declarado.

Todas las direcciones parten de `/api/deposito/envios/{envioId}/recepcion`:

| Método y sufijo | Uso |
| --- | --- |
| GET, sin sufijo | Consulta el envío, su versión, bultos declarados, escaneos recibidos y discrepancias. |
| POST `/bultos` | Registra el escaneo de recepción de un código declarado en ese envío. |
| POST `/discrepancias` | Registra una diferencia observada, del envío o de un bulto propio. |
| POST `/confirmar` | Confirma el ingreso después de recibir todos los bultos. |

Escaneo:

```json
{ "codigo": "B001", "versionEsperada": "<GUID de la consulta>" }
```

Discrepancia:

```json
{
  "versionEsperada": "<último GUID recibido>",
  "tipo": "PesoNoCoincide",
  "bultoId": "<GUID del bulto consultado>",
  "detalle": "Peso observado al recibir",
  "detalleDeclarado": "2.5 kg",
  "detalleRecibido": "3.1 kg"
}
```

Los tipos son BultoFaltante, BultoSobrante, PesoNoCoincide, DimensionesNoCoinciden,
CodigoNoCoincide y Otro. `bultoId` puede ser null para una discrepancia general o un
código sobrante que no existe en el manifiesto. Si se informa un identificador, debe
pertenecer al envío. Los tres textos son obligatorios, se recortan sus espacios exteriores
y admiten hasta 2000 caracteres. La fecha la fija el servidor en UTC.

Confirmación:

```json
{ "versionEsperada": "<último GUID recibido>" }
```

El GET y la confirmación devuelven el estado completo: envío, código de seguimiento,
versión, estado, bultos con peso y dimensiones declarados, indicador `recibido`,
discrepancias y cantidades `esperados`, `recibidos` y `faltantes`. Escaneo y discrepancia
devuelven `{ "recepcion": <ese estado>, "repetido": false }`.
Se debe conservar la nueva versión de cada respuesta para la siguiente escritura.

Respuestas: 200 éxito; 400 cuerpo, código o bulto inválidos, o falta de antiforgery;
401 sin sesión; 403 rol/usuario/operador no autorizado o envío inaccesible;
409 versión vieja o recepción no permitida. Las respuestas no se almacenan en caché.

## Reglas y reenvíos

- Solo se registra mientras el envío esté Admitido. Consultar sigue siendo posible
  después del ingreso. Un código se compara sin distinguir mayúsculas ni espacios
  exteriores y solo dentro del envío indicado.
- Un código desconocido no crea un bulto ni se asocia automáticamente a otro envío.
  Se devuelve 400; el operario puede anotarlo como discrepancia de código o sobrante.
- El peso y las dimensiones declarados se muestran para que el operario los contraste
  con lo recibido. El backend conserva la diferencia informada; no conecta una balanza,
  calcula tolerancias ni reemplaza las medidas o la tarifa original.
- Anotar un faltante no registra su recepción. El ingreso completo exige los escaneos
  de todos los bultos, como ya exigía Domain. Una diferencia de peso o dimensión no
  bloquea el ingreso si todos fueron recibidos. No se agrega una recepción parcial física.
- Reenviar un código ya recibido devuelve `repetido=true` sin escribir, incluso con la
  versión anterior, mientras la recepción siga abierta. Ocurre lo mismo con una discrepancia
  idéntica (tipo, bulto y los tres textos normalizados). No es una clave idempotente global.
- Confirmar dos veces o registrar cambios después del ingreso devuelve 409. Ante un
  conflicto nuevo hay que releer; no se mezclan escrituras concurrentes.

## Implementación y persistencia

`RecepcionDepositoHandler` en Application valida solicitudes y versiones, y llama a
`EscaneoBulto.RegistrarRecepcion`, al constructor de `Discrepancia` o a
`Envio.RecibirEnDeposito`. El repositorio en Infrastructure abre un contexto dedicado
del operador, revalida la identidad y carga el envío, su cuenta, todos los bultos con
escaneos y las discrepancias mediante una lectura Repeatable Read. Cierra esa lectura
antes de operar y conserva la versión original del agregado.

Cambiar un escaneo o una discrepancia compite por la versión del envío. Cada operación
usa un solo SaveChanges y se revierte por completo si falla. La confirmación cambia los
estados del envío y sus bultos, registra el evento Validar con origen Depósito y responsable,
y guarda su mensaje de outbox en la misma transacción. Escaneos y discrepancias solos
no crean cambios de estado ni mensajes de entrega. El backoffice de incidencias existente
ya cuenta las discrepancias guardadas, sujeto a los filtros del operador.

## Alcance y verificación

Quedan para integrar en la interfaz la selección/búsqueda del envío, lectura de cámara o
escáner, comparación visual y formularios de discrepancia y confirmación. No se agregan
pantallas ni sincronización offline.
La [asignación regular](asignacion-regular.md) está disponible como otro caso de uso.
El [cierre de devoluciones y rendición](cierre-deposito.md) tiene su propio backend.

Pruebas unitarias y contra PostgreSQL verifican recepción completa, faltantes/sobrantes,
preservación de la declaración, reenvíos, permisos/CSRF, revocación de usuario y aislamiento,
versiones concurrentes y rollback de escaneos, discrepancias y confirmación/outbox.
