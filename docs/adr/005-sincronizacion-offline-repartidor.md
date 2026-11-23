# ADR-005: trabajo sin conexión y sincronización de la app del repartidor

Estado: aceptada. La PWA usa el login, la descarga de hoja de ruta y el registro por bulto reales.
Responsable: Aldana. Actualización: 23/11/2026 (protección de la clave local, reentrega H5).

## Problema

El repartidor trabaja en la calle, con señal intermitente. No puede dejar de registrar entregas
porque se corta la red, y una acción enviada dos veces (por un reintento o una respuesta perdida)
no puede generar dos intentos de entrega en el servidor.

## Decisión

### Datos en el dispositivo

- La PWA (`src/UltimaMilla.Repartidor`, Blazor WebAssembly) guarda en IndexedDB la sesión, la hoja
  de ruta descargada (paradas, catálogo de motivos, pruebas exigidas) y la cola de acciones
  separada por repartidor.
- Toda pantalla lee del dispositivo, nunca directamente del servidor: se ve igual con o sin red.
- El service worker publicado guarda en caché los archivos de la app para que abra sin conexión.
- El acceso a IndexedDB es un módulo JS propio y chico (`wwwroot/js/repartidor.js`), sin paquetes.

### Protección del almacenamiento local

En la versión anterior la clave AES no exportable se guardaba como `CryptoKey` en IndexedDB.
Una copia del almacenamiento que incluya esa clave no exige un secreto del repartidor para
acceder a los datos. Para la reentrega H5, se elige una frase local de al menos 12 caracteres,
separada del inicio de sesión de la cuenta.

La PWA deriva una clave AES-GCM de 256 bits mediante PBKDF2 con SHA-256, 600.000 iteraciones
y una sal aleatoria de 16 bytes. IndexedDB conserva la sal, la versión de derivación y un
verificador cifrado. La clave derivada es no exportable y existe solo en la memoria de la
página. La frase se ingresa al abrir o recargar la app y el desbloqueo funciona sin red. Se
puede bloquear el dispositivo sin cerrar la sesión del servidor ni borrar visitas pendientes.
El router y la sincronización automática arrancan después del desbloqueo.

Al configurar la frase en un dispositivo existente, el navegador vuelve a cifrar los
registros con la clave derivada. Guarda esos registros, confirma la configuración y elimina
la clave anterior en una sola transacción. Si la migración falla, conserva los registros y la
clave anteriores para reintentar. Una segunda pestaña comprueba si otra ya terminó la
configuración antes de guardar.

Se eligió una frase local en lugar de un PIN corto por su mayor espacio de búsqueda al
proteger una copia del almacenamiento. Se mantiene separada de la contraseña de la cuenta
para que una renovación del token o un cambio de credenciales no bloquee los datos offline.
Sin la frase no es posible recuperar localmente las acciones pendientes. Una copia tomada
antes de la migración conserva las propiedades del formato anterior. Ver [cifrado local y prueba en navegador](../cifrado-pwa.md).

### Acciones

Cada visita completa, parcial o fallida se registra **primero en la cola local** y se envía después. Lleva:

- `IdOperacion`: GUID generado en el dispositivo. Es la clave de idempotencia. Reenviar el mismo
  cuerpo con el mismo identificador devuelve el intento existente.
- Parada y versión esperada del envío, tomadas de la hoja descargada.
- Resultado de cada bulto asignado a esa visita, con el motivo de los fallidos y las referencias
  a evidencias. El código de un bulto se puede ingresar con un lector que actúe como teclado.
- `OcurridoEn`: hora del dispositivo en UTC, tomada al registrar (no al sincronizar).
- `Posicion`: GPS opcional de la acción. Domain exige posición en cada **prueba**, así que si hay
  pruebas y no hay GPS, la app avisa y no deja registrar.

Antes de encolar se validan en el dispositivo las reglas de Domain que no dependen del servidor:
envío en tránsito y en la hoja; pruebas exigidas por el operador; motivo del catálogo; evidencia si
el motivo la exige; un resultado por bulto y una acción local por visita. Así el repartidor
corrige en el momento.

### Sincronización

`Sincronizador` envía las pendientes de la más antigua a la más nueva. Se dispara al volver la red,
cada 30 segundos, después de registrar una acción y con "Sincronizar ahora". Un semáforo impide dos
rondas simultáneas. Según la respuesta de cada operación:

| Respuesta | Estado local | ¿Se reintenta? |
| --- | --- | --- |
| Aceptada | Sincronizada | No |
| Ya procesada (`IdOperacion` repetido) | Sincronizada | No: no hay duplicado |
| Rechazada (regla de negocio / conflicto) | Rechazada, con el motivo | No |
| Sin red, timeout o error 5xx | Pendiente | Sí, con espera 5 s, 10 s, 20 s... hasta 5 min |

Ante un error de red se corta la ronda, porque las siguientes también fallarían. La espera se ignora
cuando vuelve la conexión o el repartidor toca el botón. No se puede cerrar sesión con pendientes.

### Política de conflictos: el servidor gana

Un conflicto ocurre cuando, mientras el repartidor estaba sin señal, el envío cambió en el servidor:
el despachador lo retiró de la ruta o lo reprogramó, se agotaron los intentos, no se cumplió el plazo
entre intentos o el motivo quedó inactivo.

- El servidor es la fuente de verdad: valida con el estado actual y, si no corresponde, **rechaza**.
- La app **no reintenta** un rechazo ni intenta "fusionar": la marca como Rechazada, la muestra en
  rojo con el motivo y descarga la hoja actualizada para mostrar el estado real.
- El repartidor la ve en "Sincronización", puede descartarla y, si el envío sigue en tránsito,
  registrar una nueva acción (con otro `IdOperacion`). Si no, lo resuelve el despachador.
- Un rechazo no frena el resto de la cola.

Motivo: un intento de entrega es un hecho con consecuencias (estado, intentos, avisos al cliente);
aplicar automáticamente algo que contradice una decisión del despachador es peor que pedir una
revisión. Los rechazos esperables son pocos porque la app ya valida las reglas locales.

### Login y token

La PWA corre en otro origen y no puede usar la cookie `SameSite=Strict` del backoffice; además el
login de Web rechaza a propósito el rol Repartidor. Por eso Web expone una API propia
(`src/UltimaMilla.Web/Seguridad/ApiRepartidor.cs`):

- `POST /api/repartidor/sesion` { email, contrasena }: valida con el mismo `IBuscadorUsuariosLogin`
  del login de Web (email normalizado, usuario activo, hash PBKDF2). Si además el rol es Repartidor,
  responde 200 `{ tokenType, accessToken, expiresIn, refreshToken }`. Si no, 401, sin distinguir
  email inexistente, contraseña incorrecta u otro rol.
- `GET /api/repartidor/sesion` con `Authorization: Bearer`: devuelve `{ usuarioId, email }`. La PWA lo
  usa para tomar su perfil del servidor y para validar el token antes de cada operación.

Se usa el token Bearer integrado de ASP.NET Core (`AddBearerToken`), no un JWT. Viene en el framework,
sin paquetes adicionales, y se protege con Data Protection, igual que la cookie, así que no hay una
clave de firma que guardar. Dura 8 horas, como la cookie; el refresh token no se usa todavía.
Un esquema selector usa el token solo si el pedido trae `Authorization: Bearer`; si no, la cookie de
siempre, de modo que backoffice y portal no cambian. La política `Repartidor` acepta solo el token y
el rol Repartidor. CORS habilita únicamente los orígenes de `Repartidor:Origenes` (configuración).

**Aislamiento:** el token lleva los mismos claims que la cookie (`operador_id` tomado de la base,
usuario y rol). En cada pedido, `TenantResolver` vuelve a comprobar en la base que el usuario
exista en ese operador, siga activo y conserve el rol. La PWA no guarda ni envía el operador:
no lo puede elegir. Un usuario desactivado pierde el acceso en su próximo pedido, aunque su token
todavía no haya vencido.

**Vencimiento:** la PWA guarda `ExpiraEn`. Con el token vencido, o ante un 401/403, no envía nada más.
La sesión queda marcada como vencida y la barra lo muestra; la hoja y la cola siguen disponibles
sin conexión y las acciones quedan pendientes, sin consumir reintentos. Al volver a ingresar, se
sincronizan. No se permite que otro repartidor ingrese mientras haya pendientes del anterior.

En producción, las claves de Data Protection deben persistirse y compartirse entre instancias; si no,
un reinicio invalida tokens y cookies. Ya era un requisito de la cookie.

### Contrato de Web para la PWA

La PWA no referencia proyectos del servidor (prueba de arquitectura); el contrato son los DTOs de
`src/UltimaMilla.Repartidor/Contratos`, en JSON con enums como texto (igual que la API de Web).

| Operación | Endpoint | Respuesta |
| --- | --- | --- |
| Hoja de ruta | `GET /api/repartidor/rutas` y `GET /api/repartidor/rutas/{id}` | 200 con listado o detalle de la ruta despachada |
| Intento | `POST /api/repartidor/intentos` | 200 `IntentoRegistradoDto` con `repetido`, 400 por datos inválidos, 401 por sesión, 403 por permisos, 409 por conflicto y 5xx para reintentar |

El endpoint de intentos exige Bearer con rol `Repartidor` y toma la identidad de `ITenantActual`.
La cola de la PWA envía la parada, la versión esperada, los resultados por bulto y las evidencias
con el `idOperacion` original. Consultar [el contrato de intentos](../registro-intento-aplicacion.md).

## Límite: posición con la pantalla apagada

Una PWA no puede seguir la posición en segundo plano. Con la pantalla apagada o la app minimizada,
el navegador suspende la página y `navigator.geolocation` deja de entregar posiciones. No existe una
API web de geolocalización en segundo plano; tampoco Background Sync permite leer el GPS, y su
soporte es parcial (no existe en iOS).

Mitigación adoptada, en dos partes:

1. **Posición en cada acción.** Al registrar una entrega o fallo se pide una lectura puntual (alta precisión,
   hasta 10 s) que viaja con la acción y con sus pruebas. Cada hecho relevante tiene su ubicación.
2. **Seguimiento periódico mientras la app está abierta** (16/11/2026, req. 23). `SeguimientoPosicion` toma el
   GPS cada 2 minutos si hay sesión y hoja de ruta, y guarda la lectura en `ColaPosiciones` (almacén local cifrado,
   por repartidor, tope de 720 lecturas: 24 h; al superarlo se descartan las más viejas). En cada ronda de
   sincronización, después de las entregas, se envían en lotes de hasta 200 a `POST /api/repartidor/posiciones`.
   Cada lectura lleva un `IdLectura` generado en el dispositivo: reenviar el lote tras perder la respuesta no
   duplica filas (índice único por ruta). El servidor guarda `PosicionVehiculo` (Domain/Despacho) solo para una
   ruta despachada del repartidor autenticado, con hora entre el despacho y la rendición (tolerancia de 5 min para
   el reloj del dispositivo, máximo 24 h de antigüedad). Una lectura inválida se rechaza sin tirar el lote, y lo
   que el servidor contestó sale de la cola. No es un evento de envío: no genera outbox ni cambia la versión de la
   ruta, así que no choca con el despachador.

El GPS no necesita internet: las dos partes funcionan sin conexión.

**Alcance sobre los requisitos 23 y 29.** El mapa de la flota del tablero (refresco cada 60 s) muestra, por
repartidor, la lectura más reciente entre el seguimiento periódico y los eventos con posición, con el vehículo de
su última ruta despachada y la hora de la lectura, para que el despachador sepa qué tan reciente es. **Límite que
sigue en pie:** con la pantalla apagada o la app en segundo plano el navegador suspende la página y no hay lecturas
hasta que el repartidor vuelve a abrirla. Seguir la posición con la pantalla apagada exige una app nativa o un
contenedor (por ejemplo, .NET MAUI) con permiso de ubicación en segundo plano, que la letra deja fuera para
equipos de tres al aceptar una PWA.

## Consecuencias

- El repartidor trabaja sin red y reenviar nunca duplica intentos.
- Puede pasar un tiempo hasta que el backoffice vea una entrega (consistencia eventual).
- Se confía en el reloj del dispositivo para `OcurridoEn`; el servidor puede rechazar fechas futuras.
- La sesión, la hoja de ruta y la cola permanecen cifradas con una clave derivada en memoria.
  Las fotos y las firmas se guardan cifradas con la visita. El cifrado local protege el
  almacenamiento en reposo y depende de que la frase sea robusta. Una página desbloqueada
  debe poder mostrar los datos para trabajar, incluso sin conexión.
- Si el usuario borra los datos del sitio en el navegador, se pierden las acciones no sincronizadas.
