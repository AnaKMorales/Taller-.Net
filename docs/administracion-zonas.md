# Altas y consultas de zonas y franjas horarias

Backend administrativo de la sección 5.1 de la letra original, implementado el 14/11/2026.
Permite crear zonas con sus franjas, consultarlas y agregar franjas a una zona propia.
No incluye pantallas, edición ni bajas. No requiere migración.

## Contrato de API

Solo Administrador autenticado y activo del operador. El servicio revalida usuario y rol
contra la base en cada operación. Operador y autor salen de la sesión, no del cuerpo.
Los POST exigen cookie y antiforgery obtenido de `/api/antiforgery`, enviado en
`X-CSRF-TOKEN`. Las respuestas administrativas no se guardan en caché.

| Método | Dirección | Uso |
| --- | --- | --- |
| GET | `/api/administracion/zonas` | Zonas propias con sus franjas. |
| POST | `/api/administracion/zonas` | Crea zona y al menos una franja en una sola escritura. |
| GET | `/api/administracion/zonas/{zonaId}/franjas` | Franjas de una zona propia. |
| POST | `/api/administracion/zonas/{zonaId}/franjas` | Añade una franja nueva, sin reemplazar las anteriores. |

Alta de zona:

```json
{
  "nombre": "Centro",
  "codigoPostal": "11000",
  "franjas": [
    { "nombre": "Mañana", "desde": "08:00:00", "hasta": "12:00:00" },
    { "nombre": "Tarde", "desde": "14:00:00", "hasta": "18:00:00" }
  ]
}
```

Para añadir una franja se envía un objeto como el contenido de `franjas`, sin la lista.
Textos obligatorios y recortados: nombre de zona/franja hasta 200 caracteres, código
postal hasta 20. El código postal se conserva como texto (incluidos ceros iniciales);
no se impone un formato nacional. `desde` y `hasta` son horas locales del mismo día,
sin fecha ni offset: fin estrictamente posterior a inicio, sin franjas nocturnas que
crucen medianoche. La zona no puede crearse con lista vacía, nula o elementos nulos.

Respuesta de zona: `id`, `nombre`, `codigoPostal` y `franjas`. Cada franja incluye
`id`, `nombre`, `desde`, `hasta`. Las franjas se ordenan por inicio, fin e ID; las zonas
por nombre e ID. Las altas devuelven 201 y las consultas 200. Datos/CSRF inválidos: 400;
sin sesión: 401; rol, usuario o zona inaccesibles: 403. Un ID vacío devuelve 400.
El mismo 403 para zona ajena o inexistente evita descubrir datos de otro operador.

## Relación con tarifas y envíos

Se reutilizan ZonaCobertura y FranjaHoraria. La zona pertenece a un operador y cada
franja solo puede pertenecer a una zona. Domain agrega `ZonaCobertura.AgregarFranja`,
que rechaza una franja ya vinculada; el alta administrativa crea siempre una franja nueva.
Application define `IAdministracionZonas` y los contratos; Infrastructure persiste con
EF Core, contextos dedicados, filtros y guardas existentes; Web expone la API con roles
y antiforgery. Zona y franjas iniciales se guardan juntas: un fallo no deja una zona vacía.

Las zonas nuevas aparecen en la selección del tarifario y en las opciones de alta del
comercio existentes. **Crear una zona no le asigna precio**: para cotizarla debe tener
una línea aplicable en el tarifario vigente del operador. Esto se configura mediante
la administración versionada de tarifas existente, que mantiene su invalidación de caché.
No se requiere invalidar el tarifario al crear una zona o agregar una franja: estas
operaciones no modifican sus líneas ni sus precios. Tampoco generan eventos de envío.

Añadir una franja permite ofrecer otra ventana en futuras cotizaciones. No cambia las
franjas existentes, la programación elegida, las ventanas ni la tarifa de un envío ya
guardado. Dos adiciones simultáneas se insertan como filas independientes, sin reemplazar
la colección ni perder las franjas anteriores. No se envía una versión esperada porque
este bloque no edita ni elimina filas existentes.

## Límites y verificación

Se conserva la ausencia de unicidad de nombre/código postal y la posibilidad de franjas
superpuestas del modelo actual. No se inventan reglas nuevas de exclusión. Los POST no
son idempotentes: repetirlos crea otros registros, incluso con los mismos datos. Ante una
respuesta perdida, consultar antes de repetir; las referencias válidas siempre son los IDs.

Quedan pantallas, edición/bajas y cualquier futura política de desactivación, deduplicación
o versionado del catálogo. Antes de habilitar cambios o bajas deberá revisarse su efecto
en tarifas y programaciones históricas y su control de concurrencia. Este bloque no
modifica PWA, Worker ni las interfaces de otras integrantes.

Las pruebas verifican permisos/CSRF, validaciones, aislamiento y revocación, rollback de
zona/franjas, adiciones concurrentes y el recorrido de nueva zona, tarifario, opciones,
cotización y alta real por API conservando el envío anterior. Hay además una prueba de
dominio que impide reutilizar una franja vinculada y conserva las anteriores.
