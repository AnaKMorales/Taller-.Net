# ADR-005: trabajo sin conexión y sincronización de la app del repartidor

Estado: aceptada. Login real implementado en Web; hoja de ruta e intentos siguen simulados en la PWA.

## Problema

El repartidor trabaja en la calle, con señal intermitente. No puede dejar de registrar entregas
porque se corta la red, y una acción enviada dos veces (por un reintento o una respuesta perdida)
no puede generar dos intentos de entrega en el servidor.

## Decisión

### Datos en el dispositivo

- La PWA (`src/UltimaMilla.Repartidor`, Blazor WebAssembly) guarda en IndexedDB la sesión, la hoja
  de ruta descargada (paradas, catálogo de motivos, pruebas exigidas) y la cola de acciones.
- Toda pantalla lee del dispositivo, nunca directamente del servidor: se ve igual con o sin red.
- El service worker publicado guarda en caché los archivos de la app para que abra sin conexión.
- El acceso a IndexedDB es un módulo JS propio y chico (`wwwroot/js/repartidor.js`), sin paquetes.

### Acciones

Cada entrega exitosa o fallo se registra **primero en la cola local** y se envía después. Lleva:

- `IdOperacion`: GUID generado en el dispositivo. Es la clave de idempotencia; `Envio.RegistrarIntento`
  ya devuelve el intento existente si recibe un `IdOperacion` repetido con el mismo resultado.
- `OcurridoEn`: hora del dispositivo en UTC, tomada al registrar (no al sincronizar).
- `Posicion`: GPS opcional de la acción. Domain exige posición en cada **prueba**, así que si hay
  pruebas y no hay GPS, la app avisa y no deja registrar.

Antes de encolar se validan en el dispositivo las reglas de Domain que no dependen del servidor:
envío en tránsito y en la hoja; pruebas exigidas por el operador; motivo del catálogo; evidencia si
el motivo la exige; un solo resultado por envío. Así el repartidor corrige en el momento.

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

### Contrato pendiente en Web

La PWA no referencia proyectos del servidor (prueba de arquitectura); el contrato son los DTOs de
`src/UltimaMilla.Repartidor/Contratos`, en JSON con enums como texto (igual que la API de Web).

| Operación | Endpoint propuesto | Respuesta |
| --- | --- | --- |
| Hoja de ruta | `GET /api/repartidor/hoja-de-ruta` | 200 `HojaDeRutaDto` de la ruta activa |
| Acción | `POST /api/repartidor/intentos` `AccionEntregaDto` | 200 `RespuestaAccion` (Aceptada/YaProcesada) / 409 o 422 con detalle = Rechazada / 401 = sesión vencida / 5xx = reintentar |

Estos endpoints deben exigir la política `Repartidor`, tomar operador y repartidor de `ITenantActual`
(nunca del cuerpo) y llamar a `Envio.RegistrarIntento` con el `IdOperacion` recibido. Si ese
`IdOperacion` ya existe con otro resultado, responden Rechazada. Mientras no existan,
`ApiRepartidorHttp` valida el token real y delega en la simulación.

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
  desbloqueado puede leerlas. Cifrado, firma, foto y escaneo quedan para la segunda etapa.
- Si el usuario borra los datos del sitio en el navegador, se pierden las acciones no sincronizadas.
