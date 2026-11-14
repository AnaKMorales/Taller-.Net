# Alta de envíos por API para comercios

Requisito 7 de la letra original, sección 5.2; cálculo de tarifa del requisito 9.
Backend implementado el 14/11/2026 para integraciones servidor a servidor.
Reutiliza el alta del portal y la idempotencia de importación. No requiere migración nueva;
la base debe tener aplicada `ImportacionIdempotente`, como para la importación existente.

## Autenticación

`POST /api/comercios/sesion` recibe las credenciales de un usuario de comercio existente:

```json
{ "email": "integracion@comercio.example", "contrasena": "<contraseña configurada>" }
```

Se usa el mismo validador de credenciales y hash de contraseñas de la aplicación. El usuario
debe estar activo, ser de rol Comercio y tener exactamente una cuenta comercial asociada.
Emails ambiguos entre operadores, usuarios sin cuenta o con varias cuentas y credenciales
incorrectas reciben 401. No se elige una cuenta arbitrariamente ni se agrega un selector.
La API no crea usuarios ni asigna contraseñas.

La respuesta del manejador Bearer existente incluye `tokenType`, `accessToken`, `expiresIn`
y `refreshToken`. El access token dura ocho horas. Esta API no agrega un endpoint de refresh:
al vencer, se inicia otra sesión. Usar HTTPS y enviar `Authorization: Bearer <accessToken>`
en las consultas y altas. Los tokens son protegidos por Data Protection, como los de la PWA;
no son JWT ni claves permanentes de integración. La configuración persistente de claves
existente permite conservar su validez durante los reinicios dentro de su vigencia.

La política exige autenticación Bearer y rol Comercio; no utiliza la cookie del portal ni
requiere token antiforgery. El operador, usuario y cuenta se recuperan de claims emitidos
por el servidor y se validan contra la base en cada pedido. La persistencia vuelve a validar
usuario y pertenencia antes de crear o devolver un alta anterior. Una revocación impide
continuar incluso con un token no vencido. No se agregó CORS para navegadores externos.

El login admite diez solicitudes por minuto y dirección IP en cada instancia; el exceso
recibe 429. Las respuestas de login y de negocio llevan `Cache-Control: no-store`.

## Recorrido

1. `GET /api/comercios/envios/opciones`: zonas/franjas del operador y modalidades/plazos.
2. `POST /api/comercios/envios/cotizacion`: calcula tarifa, compromiso y opciones de entrega.
3. `POST /api/comercios/envios`: confirma una opción con referencia estable del pedido.

Ejemplo de cuerpo para cotizar:

```json
{
  "zonaId": "<GUID de opciones>",
  "modalidad": "Estandar",
  "destinatarioNombre": "Persona",
  "destinatarioTelefono": "099123456",
  "destinatarioEmail": "persona@example.com",
  "calle": "Calle",
  "numero": "100",
  "localidad": "Montevideo",
  "codigoPostal": "11000",
  "bultos": [{ "codigo": "CAJA-1", "peso": 2.5, "alto": 20, "ancho": 30, "largo": 40 }]
}
```

Peso en kg y dimensiones en cm. La cotización devuelve `tarifa`, `fechaComprometida`
y `opciones`, cada una con su `clave`, fecha, franja y ventana concreta. Para crear:

```json
{
  "referencia": "PEDIDO-123",
  "solicitud": { "...": "el mismo objeto completo enviado al cotizar" },
  "claveEntrega": "<clave de una opción recibida>"
}
```

El objeto abreviado de ese ejemplo se sustituye por la solicitud completa anterior.
El cliente elige una opción, no la tarifa ni las fechas de compromiso. Al confirmar, el
servidor recalcula con las reglas y el tarifario vigentes; una cotización previa no congela
el precio. La configuración y tarifa quedan conservadas al crear el envío.

El resultado es:

```json
{
  "referencia": "PEDIDO-123",
  "repetido": false,
  "envio": {
    "codigoSeguimiento": "<código generado>",
    "tarifa": 165,
    "versionTarifario": 1,
    "fechaComprometida": "<instante>",
    "entrega": { "...": "opción confirmada" }
  }
}
```

## Referencia e idempotencia

La referencia es obligatoria, de 1 a 100 caracteres después de recortar espacios exteriores,
y distingue mayúsculas. Se comparte con la importación por operador/cuenta: el mismo pedido
enviado por API y luego importado con esa referencia y contenido se reconoce como existente.
No se agrega un prefijo por canal. Conviene usar el identificador estable del pedido y
conservar el mismo contenido y `claveEntrega` al reenviar.

| Situación | HTTP | Resultado |
| --- | --- | --- |
| Primera alta válida | 201 | `repetido=false` y resultado calculado. |
| Mismos datos y referencia dentro de la cuenta | 200 | `repetido=true` y respuesta original, sin nuevo envío/evento/outbox. |
| Misma referencia con otros datos | 409 | No se modifica el envío anterior. |
| Datos, zona, bultos, referencia u opción inválidos | 400 | No se crea el envío. |
| Sin token válido | 401 | Se requiere iniciar sesión. |
| Otro rol o ámbito revocado | 403 | Operación denegada. |
| Falla técnica al guardar | 500 | Se revierte el alta completa; se puede reenviar conservando la referencia. |

La referencia de otra cuenta u operador es independiente. La comparación de contenido usa
la huella existente: incluye destinatario/dirección, modalidad, zona, opción y bultos; normaliza
la escala decimal y el orden de bultos. No permite cambiar datos usando la referencia anterior.
La respuesta original puede recuperarse después de vencer su ventana de entrega con una sesión
vigente, porque la idempotencia se resuelve antes de recalcular nuevas opciones.

## Implementación y alcance

Web define el contrato HTTP y traduce los resultados. Las opciones y la cotización usan
`AltaEnvioPortalHandler`; el alta usa una fila de `ImportarEnviosHandler` y `ImportacionEnvios`.
Se reutilizan el bloqueo transaccional por referencia, el índice único y el registro
`AltasImportadas`. Alta, historial, outbox y registro de referencia se confirman o revierten
juntos. Los identificadores de operador/cuenta y un precio que se agreguen al JSON no
sustituyen el ámbito autenticado ni el cálculo del servidor.

Este avance cubre el alta exigida por el requisito 7. No acredita por sí solo todo el opcional
de API pública: no agrega API keys, administración de credenciales, consultas generales,
importación de archivos ni pantallas. Las integraciones del portal siguen a cargo de Web.

Pruebas con PostgreSQL y autenticación real verifican credenciales/roles, revocación,
cuentas ambiguas, límite de login, cálculo, separación por cuenta/operador, reenvíos entre
API/importación, concurrencia y rollback. También se ejecuta la regresión del alta/importación
y se mide la cobertura habitual.
