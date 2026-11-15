# Altas y consultas de comercios, cuentas y usuarios

Bloque de backend de administración del operador de la sección 5.1 de la letra original.
Implementado el 14/11/2026. Las altas/consultas y la primera etapa de usuarios no necesitaron
migración; la edición/baja de cuentas incorpora `AdministracionCuentaComercial`.
La revisión de perfiles/permisos de A1 está cubierta en el backend descrito aquí;
la presentación todavía debe integrarse por las responsables de Web.

## Autorización

Solo Administrador autenticado, activo y del operador actual. Cada método revalida al
administrador contra la base. La cuenta del cliente no se recibe como ámbito de sesión
de un comercio: estas operaciones son del personal administrador del operador.
Los POST y PUT usan cookie y antiforgery de `/api/antiforgery`, enviado en `X-CSRF-TOKEN`.
Las respuestas no se almacenan en caché.

## API

| Método | Dirección | Uso |
| --- | --- | --- |
| GET | `/api/administracion/comercios` | Lista las cuentas del operador con sus datos de comercio. |
| POST | `/api/administracion/comercios` | Crea un comercio nuevo y su cuenta con ese operador, en una sola escritura. |
| GET | `/api/administracion/comercios/{cuentaId}/usuarios` | Lista usuarios vinculados a esa cuenta propia. |
| POST | `/api/administracion/comercios/{cuentaId}/usuarios` | Crea un usuario de rol Comercio, contraseña y vínculo con esa cuenta. |

Alta de comercio/cuenta:

```json
{ "razonSocial": "Comercio", "telefono": "099123456", "email": "contacto@comercio.example" }
```

Los textos se recortan. Razón social obligatoria de hasta 200 caracteres; teléfono
obligatorio de hasta 50; email válido sin nombre de presentación, normalizado a minúsculas
y de hasta 254 caracteres. El email de contacto no crea una credencial de acceso.

La respuesta 201 incluye `cuentaId`, `comercioId`, `razonSocial`, `telefono`, `email`, `activa` y `version`.
El listado devuelve esos mismos datos, solo para las cuentas del operador autenticado.
**Las operaciones de usuarios reciben `cuentaId`, no `comercioId`.**

Alta de usuario de la cuenta:

```json
{ "email": "usuario@comercio.example", "contrasena": "<contraseña de 8 a 256 caracteres>" }
```

Se crea un usuario activo de rol Comercio y vinculado únicamente a la cuenta de la URL.
El rol no se elige desde el cuerpo. El email tiene las mismas reglas de formato y no puede
pertenecer a otro usuario del operador, aunque tenga otro rol o esté inactivo. Si otra
solicitud gana la carrera por el mismo email, se devuelve conflicto y se revierte el alta
perdedora. No se reutiliza el usuario existente ni se le agrega silenciosamente otra cuenta.

La contraseña usa el hash existente del proyecto. Usuario, hash y vínculo se confirman
juntos. La respuesta 201 y los listados solo muestran `id`, `email` y `activo`, sin secretos.
El usuario puede usar el login existente del portal y el [login de API](api-comercios.md),
que exige una única cuenta comercial. El login conserva su rechazo de emails ambiguos
entre operadores; este avance no agrega selección de operador/cuenta.

Respuestas: 200 consulta; 201 alta; 400 datos/antiforgery inválidos; 401 sin sesión;
403 rol, usuario o cuenta inaccesibles; 409 email ocupado. Un fallo técnico revierte toda
la escritura que falló y no deja comercios huérfanos o usuarios sin vínculo/credenciales.

## Comercio compartido y recuperación de errores

### A1, primera etapa: modificación y baja de usuarios

| Método | Dirección | Uso |
| --- | --- | --- |
| PUT | `/api/administracion/comercios/{cuentaId}/usuarios/{usuarioId}` | Modifica únicamente el email de un usuario de Comercio activo. |
| POST | `/api/administracion/comercios/{cuentaId}/usuarios/{usuarioId}/baja` | Desactiva al usuario conservando ID, vínculos, hash e historial. |

Modificación:

```json
{ "email": "nuevo@comercio.example", "esperado": { "email": "anterior@comercio.example", "activo": true } }
```

Baja:

```json
{ "email": "anterior@comercio.example", "activo": true }
```

El estado esperado se copia del último listado, incluyendo el email exactamente como
se recibió. Ambos campos son obligatorios. Respuesta 200 con el mismo DTO de usuario.
Se mantienen autorización de administrador activo y antiforgery. El destino debe ser
de rol Comercio y pertenecer a la cuenta indicada. No se pueden modificar roles,
contraseñas ni vínculos mediante estos endpoints. Usuario ajeno, inexistente o de otro
rol: 403. Usuario vinculado a varias cuentas: 409; no se cambia un acceso compartido
desde una sola cuenta. Usuario inactivo no admite cambios de email: 409.

Una transacción bloquea la fila del usuario dentro del operador antes de leer y comparar
su email/estado con lo esperado. Si otro administrador lo cambió, devuelve 409 en lugar
de sobrescribirlo. Se compara estado, no una versión monotónica: no detecta cambios que
volvieron a los mismos valores (ABA). No se agregaron columnas ni migraciones. El índice
existente protege la unicidad del email también entre dos usuarios editados simultáneamente.
Un fallo revierte la escritura completa; el cambio de email conserva contraseña y cuenta.

La baja es lógica: el usuario permanece en los listados como inactivo, su nuevo login
se rechaza y las operaciones que revalidan su identidad rechazan tokens anteriores
(la API de comercios devuelve 403). No se cambia el comercio ni se dan de baja otros
usuarios de la cuenta. Repetir la baja con el estado antiguo produce 409; tras releer
el estado inactivo, repetirla con ese estado confirma la baja sin eliminar registros.
No se agregó reactivación ni restablecimiento de contraseña.

### A1, segunda etapa: datos y baja de la cuenta comercial

| Método | Dirección | Uso |
| --- | --- | --- |
| PUT | `/api/administracion/comercios/{cuentaId}` | Modifica razón social, teléfono y email de contacto de esa cuenta propia. |
| POST | `/api/administracion/comercios/{cuentaId}/baja` | Deshabilita la cuenta sin borrar vínculos ni historial. |

Modificación:

```json
{ "razonSocial": "Sucursal", "telefono": "099123456", "email": "contacto@comercio.example", "versionEsperada": "<version del listado>" }
```

Baja: `{"versionEsperada":"<version del listado>"}`. Los límites/formato de texto
son los del alta. Respuesta 200 con el DTO de cuenta, incluyendo `activa` y `version`.
Versión vacía o datos inválidos: 400; cuenta inaccesible: 403; versión desactualizada o
modificación de cuenta inactiva: 409. EF usa Version como token de concurrencia: dos
escrituras con la misma versión no pueden confirmar juntas. Cada cambio genera otra
versión. Para repetir una baja, releer: con la versión actual de una cuenta inactiva,
confirmarla no genera otra versión. Se siguen listando cuentas inactivas para administración.

**Se editan datos locales de CuentaComercial**, no los datos globales de Comercio.
La migración copia razón social/teléfono/email actuales a cada cuenta, mantiene todas
activas y asigna versiones. Si dos operadores comparten un Comercio, cada uno puede
actualizar su ficha sin afectar la del otro. Los nombres mostrados en reportes, detalle
y avisos del backoffice usan la ficha de la cuenta. Esos nombres son actuales; no son
una instantánea histórica del nombre al admitir cada envío. Tarifas y programaciones
históricas siguen conservándose como antes. No se altera identidad fiscal ni se
fusionan/desvinculan comercios globales.

La baja bloquea nuevos logins de esa cuenta, resolución de sus sesiones anteriores,
escrituras autenticadas con un contexto anterior y nuevas altas de envíos. También
impide crearle nuevos usuarios. No desactiva físicamente cada usuario: si tiene otra
cuenta activa puede seguir accediendo a ella. El login considera solo las cuentas
activas y conserva el rechazo cuando hay varias posibles. No se agregó reactivación.

Un trigger de PostgreSQL valida y bloquea la fila de la cuenta al insertar un envío:
serializa la comprobación con la baja incluso ante SQL directo. Si la baja confirmó
primero, se rechaza el INSERT; si el alta ganó antes, la baja espera y ese envío queda
como historial previo. El rechazo concreto se traduce en operación no permitida.
No bloquea cambios de envíos existentes, lectura administrativa, seguimiento público,
ni mensajes pendientes de Worker. No se cancelan envíos en curso ni avisos ya comprometidos.

Aplicar con el comando habitual, antes de arrancar la versión nueva:

```powershell
dotnet ef database update --project src/UltimaMilla.Infrastructure --configuration Release
```

`docs/database-initial.sql` incluye el esquema actualizado para una instalación desde cero.
No ejecutar ese script completo sobre una base existente: usar las migraciones.

Se conserva el modelo: Comercio guarda datos comunes y CuentaComercial relaciona ese
comercio con un operador. Los comercios compartidos existentes siguen siendo consultables
por cada operador mediante su propia cuenta. Un administrador no ve los usuarios o cuentas
del otro operador ni puede modificar sus datos globales.

El POST de comercio crea un comercio nuevo; no recibe un identificador de comercio ajeno,
no busca globalmente por email/razón social y no agrega un enlace arbitrario a otro operador.
La vinculación segura de un comercio global existente queda fuera de este bloque.

No se inventó una unicidad global por nombre o email de contacto, porque el modelo no
tiene un identificador fiscal único. Repetir POST de comercio crea otro comercio/cuenta;
no es una operación idempotente. Si se perdió una respuesta, consultar antes de repetir.
La creación de usuario es un segundo paso: si falla, la cuenta ya creada permanece y
se reintenta ese paso, sin volver a crear el comercio.

## Implementación y pendientes

### Perfiles y permisos de A1

Se usa autorización por roles (RBAC), junto con el ámbito obligatorio de operador y
cuenta. El perfil de los usuarios de un comercio es `Comercio`: se asigna en el servidor
al crearlos. No se transforman en personal del operador al modificar su email. Los
campos extra como `rol`, `permisos` u otro `cuentaComercialId` no son parte del contrato
de alta/modificación y no cambian la identidad asignada. No existe un editor de permisos
individuales ni perfiles comerciales adicionales en esta implementación.

| Perfil | Administración de comercios/cuentas/usuarios | Operación comercial |
| --- | --- | --- |
| Administrador activo del operador | Alta, consulta, modificación y baja de cuentas y usuarios dentro de los límites documentados. Escrituras con antiforgery. | No obtiene una sesión de Comercio por tener rol Administrador. |
| Despachador, operario de depósito, repartidor | Denegada, aunque puedan acceder a otras funciones del backoffice. | No obtienen acceso de Comercio por su perfil interno. |
| Comercio | Denegada; no administra su propia cuenta ni se autoasigna privilegios. | Puede usar las operaciones del portal/API existentes sobre su cuenta activa; no elegir otra cuenta u operador desde el cuerpo. |
| Anónimo, usuario inactivo o cuenta comercial inactiva | Sin acceso autorizado. | Se rechaza login o resolución de identidad según corresponda. |

Las pruebas verifican los ocho endpoints administrativos frente a cada perfil interno
no administrador y prueban con Bearer real el perfil Comercio y la imposibilidad de
elevar privilegios mediante campos extra. El control vuelve a validar los datos de
identidad en la base; no se acredita autorización solamente por ocultar botones.
La letra exige asignación de perfiles/permisos, pero no prescribe un diseñador de roles
ni varios perfiles comerciales; ese alcance adicional no se incorpora como pendiente
obligatorio de A1 sin un requerimiento concreto del equipo/docente.

### Estado de cierre

El backend de A1 queda cubierto para el perfil comercial y operaciones documentadas:
alta/consulta, modificación de ficha local y baja de cuenta, alta/consulta de usuarios,
cambio de email y baja lógica, con permisos, aislamiento y concurrencia verificados.
No equivale a cerrar las pantallas o certificar toda la sección de autenticación (V1).

Application define `IAdministracionComercios` y contratos; Infrastructure implementa
`AdministracionComercios` con contextos dedicados, filtros/guardas del operador, constructores
de Domain, validación de datos y SaveChanges. No usa consultas sin filtro para descubrir
comercios globales ni relaja la protección de datos compartidos. El alta de un cliente
no genera un evento de envío; el outbox se crea cuando ese cliente da de alta un envío.

Quedan las pantallas para completar el requisito de extremo a extremo. Reactivación,
cambio de contraseña, asociación de usuarios existentes a otras cuentas y roles/permisos
adicionales son límites/extensiones fuera de este cierre, no nuevos pendientes automáticos.
Las [altas/consultas de zonas y franjas](administracion-zonas.md) tienen su propio backend.
No se modificaron PWA ni Worker.

Pruebas con PostgreSQL verifican alta administrativa hasta login y creación de envío en la
cuenta nueva, roles/CSRF, validaciones, duplicados concurrentes, comercio compartido entre
operadores, cuentas ajenas, revocación y rollback de comercio/cuenta y usuario/credenciales.
La etapa A1 verifica además el login con email nuevo y contraseña original, la revocación
de un Bearer emitido antes de la baja, el rechazo de cuentas múltiples y cambios con
estado obsoleto, las carreras edición/baja y por email, y rollback de modificación/baja.
