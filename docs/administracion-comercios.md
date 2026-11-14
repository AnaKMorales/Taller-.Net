# Altas y consultas de comercios, cuentas y usuarios

Bloque de backend de administración del operador de la sección 5.1 de la letra original.
Implementado el 14/11/2026, sin migración nueva. Este avance incorpora altas y consultas;
no completa edición, bajas ni todas las operaciones administrativas.

## Autorización

Solo Administrador autenticado, activo y del operador actual. Cada método revalida al
administrador contra la base. La cuenta del cliente no se recibe como ámbito de sesión
de un comercio: estas operaciones son del personal administrador del operador.
Los POST usan cookie y antiforgery de `/api/antiforgery`, enviado en `X-CSRF-TOKEN`.
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

La respuesta 201 incluye `cuentaId`, `comercioId`, `razonSocial`, `telefono` y `email`.
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

Application define `IAdministracionComercios` y contratos; Infrastructure implementa
`AdministracionComercios` con contextos dedicados, filtros/guardas del operador, constructores
de Domain, validación de datos y SaveChanges. No usa consultas sin filtro para descubrir
comercios globales ni relaja la protección de datos compartidos. El alta de un cliente
no genera un evento de envío; el outbox se crea cuando ese cliente da de alta un envío.

Quedan las pantallas, edición/bajas, desactivación/reactivación o cambio de contraseña,
asociación de usuarios existentes a otras cuentas y gestión de roles/permisos adicionales.
Las [altas/consultas de zonas y franjas](administracion-zonas.md) tienen su propio backend.
No se modificaron PWA ni Worker.

Pruebas con PostgreSQL verifican alta administrativa hasta login y creación de envío en la
cuenta nueva, roles/CSRF, validaciones, duplicados concurrentes, comercio compartido entre
operadores, cuentas ajenas, revocación y rollback de comercio/cuenta y usuario/credenciales.
