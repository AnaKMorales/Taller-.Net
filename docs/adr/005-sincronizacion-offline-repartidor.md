# ADR-005: trabajo sin conexión y sincronización de la app del repartidor

Estado: aceptada. La PWA usa el login, la descarga de hoja de ruta y el registro por bulto reales.

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

### Conflictos por bulto (entrega parcial)

Una visita con varios bultos viaja como **una sola operación**: un `IdOperacion`, la parada, la
versión esperada del envío y el resultado de *todos* los bultos de esa parada
(`IntentoPorBultoDto`). El detalle de diseño está en el [ADR-007](007-entrega-parcial.md).

**Qué controla la PWA antes de guardar** (`Sincronizacion/ValidadorAccion.cs`, `ValidarPorBulto`):

- La parada y su versión coinciden con la hoja guardada, y todos sus bultos siguen `EnTransito`
  (`ParadaDto.AdmiteRegistro`). Si no, pide actualizar la hoja.
- Hay un resultado, Entregado o No entregado, para cada bulto de la parada, ni uno más ni uno menos.
- Si las reglas no permiten entrega parcial, todos los bultos llevan el mismo resultado.
- Cada bulto entregado tiene las pruebas que exige el operador. Cada no entregado tiene un motivo
  del catálogo, y la evidencia si el motivo la pide.
- Las evidencias tienen posición y entre todas no pasan de 4 MB.

**Qué puede rechazar igual el servidor:** lo que dependa del estado actual. Por ejemplo, el
despachador sacó un bulto de la visita o lo mandó a otra parada, otra operación ya cerró esos
bultos, se agotaron los intentos o el motivo quedó inactivo. El handler
(`Application/Envios/RegistrarIntento/RegistrarIntentoHandler.cs`) y `Envio.RegistrarIntentoPorBulto`
(`Domain/Envios/Envio.IntentosBulto.cs`) validan todo antes de modificar algo.

**Qué pasa si el servidor rechaza un bulto:** se rechaza **la visita completa**. Nunca se guardan
"los bultos que sí estaban bien". La respuesta HTTP depende de la causa:

| Causa | Respuesta | Qué muestra la PWA en "Sincronización" |
| --- | --- | --- |
| Cambió la versión del envío, o un bulto cambió de estado o salió de la parada (`ConflictoIntentoException`) | 409 con `conflicto`: el detalle por bulto (`BultoId`, `Codigo`, estado actual y motivo: `EstadoCambio`, `ParadaCerrada`, `FueraDeParada` o `SinCambioDeEstado`) | "La versión ya no está vigente… Actualizá la hoja antes de volver a registrar." |
| Una regla de negocio no se cumple (`InvalidOperationException`, por ejemplo faltan pruebas o el motivo no es válido) | 409 "Operación no permitida" con el mensaje de la regla en `detail` | El mensaje de la regla |
| Datos mal formados (`ArgumentException`) | 400 con el mensaje en `detail` | El mensaje |

En los tres casos `ApiRepartidorHttp` devuelve `Rechazada`, y el `Sincronizador` marca la acción
como Rechazada y no la reintenta. El rechazo no frena las demás visitas de la cola.

**Qué ve el repartidor:**

1. La parada muestra el distintivo rojo "Rechazada" (`Componentes/EstadoParada.razor`).
2. En "Sincronización" (`Pages/Pendientes.razor`) ve el motivo y el botón "Entendido, descartar".
3. Como hubo un rechazo, `SincronizacionAutomatica` descarga la hoja de nuevo. Así la parada
   muestra la versión y la composición actuales.
4. Si todos los bultos de la parada siguen en tránsito, puede registrar la visita de nuevo. Sale
   con otro `IdOperacion` y con la versión nueva. Si no (la parada se cerró o el bulto se
   reasignó), la parada queda "disponible para consulta" y lo resuelve el despachador.

**Limitación actual:** la PWA lee solo `title` y `detail` del problema. El detalle por bulto que
manda el servidor en `conflicto` todavía no se muestra, así que el repartidor no ve *cuál* bulto
causó el rechazo. Está anotado como pendiente en el ADR-007.

**Por qué gana el servidor también por bulto:** cerrar la visita cambia a la vez el estado de los
bultos, el del envío, la parada, la ruta y el evento del outbox que dispara los avisos. Si se
aplicaran solo los bultos "válidos", podría quedar un cierre con evidencias incompletas y avisar al
comercio un resultado distinto del que registró el repartidor. Además, el servidor sabe cosas que
el dispositivo no puede saber sin conexión: reasignaciones del despachador, intentos de otro
dispositivo, versiones nuevas. Rechazar todo y pedir que se registre de nuevo con la hoja
actualizada es más lento, pero nunca deja un estado a medias.

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

Mitigación adoptada: **tomar la posición en cada acción**. Al registrar una entrega o fallo se pide
una lectura puntual (alta precisión, hasta 10 s) que viaja con la acción y con sus pruebas. Así cada
hecho relevante tiene su ubicación, aunque no haya un recorrido continuo. El GPS no necesita internet,
así que funciona sin conexión. Si en el futuro se necesita seguimiento continuo, la alternativa es
una app nativa o un contenedor (por ejemplo, .NET MAUI) con permiso de ubicación en segundo plano.

## Consecuencias

- El repartidor trabaja sin red y reenviar nunca duplica intentos.
- Puede pasar un tiempo hasta que el backoffice vea una entrega (consistencia eventual).
- Se confía en el reloj del dispositivo para `OcurridoEn`; el servidor puede rechazar fechas futuras.
- La sesión y la cola en IndexedDB **no están cifradas** todavía: quien tenga el dispositivo
  desbloqueado puede leerlas. Las fotos y las firmas se guardan con la visita para poder
  capturarlas sin conexión.
- Si el usuario borra los datos del sitio en el navegador, se pierden las acciones no sincronizadas.
- Las limitaciones de la plataforma (GPS en segundo plano, cifrado, cámara, almacenamiento, iOS y
  push) están en el [ADR-008](008-limitaciones-pwa-repartidor.md).
