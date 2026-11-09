# ADR-003 Modo de renderizado de Blazor

Estado: aceptado e implementado en el seguimiento público y el portal del comercio.
Responsable: Ana Morales. Fecha: 08/11/2026.
Origen: sección 6.2 de la letra; decisión abierta en la sección 7.2 del documento inicial del Grupo 5,
que proponía Interactive Server para el portal y SSR para el seguimiento.

## Contexto y restricciones

La letra exige Blazor para el **portal del comercio** y el **seguimiento público**, y deja el modo de
renderizado a criterio del equipo, con la condición de analizar sus consecuencias sobre el escalado
horizontal, el manejo de estado, la latencia y el consumo de recursos del servidor (6.2).

Restricciones que pesan en la decisión:

- **Aislamiento por operador y por comercio (6.5).** El operador y la cuenta comercial se resuelven en
  un middleware de `Program.cs`, **una vez por pedido HTTP**, a partir de los claims del usuario. El
  resultado se guarda en `HttpContext.Items` y lo lee `TenantActualHttp`, del que dependen los filtros
  globales de EF Core. Todo el aislamiento supone que hay un `HttpContext` por cada operación.
- **API sin estado en memoria (6.13)** y protección de datos compartida entre instancias.
- **El seguimiento público** es el componente con más tráfico y el único sin autenticación: necesita
  caché, limitación de tasa por IP y una superficie mínima (4 y 6.3).
- **El portal** son listados y formularios: alta de suscripciones, desactivar, reenviar avisos. No
  necesita actualizaciones en tiempo real; el tiempo real del sistema es el tablero del backoffice,
  que usa SignalR propio (6.10).
- Equipo de tres integrantes, con poco tiempo: se prefiere el modo con menos piezas que mantener.

## Decisión

**Renderizado estático en el servidor (static SSR)** para el seguimiento público y para el portal del
comercio. Los componentes no declaran `@rendermode`.

| Pantalla | Modo | Interacción |
| --- | --- | --- |
| Seguimiento público (`/seguimiento/{token}`) | Static SSR | Solo lectura |
| Portal: Mis envíos (`/portal`) | Static SSR | Solo lectura |
| Portal: Avisos (`/portal/avisos`) | Static SSR | Formularios con `@formname`, `<AntiforgeryToken />` y POST normal; *Post-Redirect-Get* en bajas y reenvíos |

Cómo funciona: cada visita y cada envío de formulario es un pedido HTTP independiente. El servidor
renderiza el componente, devuelve HTML y no guarda nada. Los formularios usan el *form handling*
de Blazor (`@formname`, `@onsubmit`, `[SupplyParameterFromForm]`). La autorización se declara con
`[Authorize]` en el componente y la aplica el middleware sobre el endpoint, igual que en Razor Pages.

Excepciones:

- **`/envios/nuevo`** (`NuevoEnvio.razor`, entrega del 8/10) sigue en `InteractiveServer`. Es
  transitorio: está pendiente pasarlo a SSR por el mismo motivo de aislamiento descrito abajo.
- **La aplicación del repartidor** es Blazor WebAssembly como PWA. No es una elección de este ADR:
  operar sin conexión obliga a que el código corra en el dispositivo.
- **El backoffice** es Razor Pages por exigencia de la letra.

## Alternativas consideradas

**Interactive Server (la propuesta inicial para el portal).** El componente queda vivo en el servidor
y cada clic viaja por un circuito SignalR. Se descartó por tres motivos:

1. **Aislamiento.** Después de la carga inicial, las acciones del usuario llegan por el circuito y no
   pasan por el pipeline HTTP: el middleware que resuelve el operador no se ejecuta y no hay un
   `HttpContext` por acción. Microsoft desaconseja usar `IHttpContextAccessor` dentro de componentes
   interactivos. Habría que mantener un segundo mecanismo de tenancy (por ejemplo, un servicio con
   alcance de circuito que se completa desde `AuthenticationStateProvider`) y probar que nunca queda
   desactualizado. Es justamente el riesgo de filtración entre comercios que el ADR-002 busca evitar.
2. **Escalado y estado.** Cada usuario conectado ocupa un circuito en la memoria de una instancia.
   Con varias instancias hace falta afinidad de sesión en el balanceador, y si una instancia se cae,
   sus usuarios pierden el estado de la pantalla. Contradice la API sin estado de la sección 6.13 y
   el escenario 1 de la defensa (bajar una instancia con tráfico).
3. **Recursos.** Una conexión WebSocket y memoria por usuario mientras tenga la pestaña abierta,
   aunque solo esté mirando un listado.

Su ventaja, la interactividad sin recargar (validación en vivo, refresco automático), no hace falta
en estas pantallas.

**Interactive WebAssembly.** El componente corre en el navegador. Elimina el estado en el servidor,
pero obliga a exponer una API HTTP aparte para cada operación del portal, con su propia autorización
y su propio manejo del tenant, y a descargar el runtime de .NET (varios MB) en la primera visita. Para
el seguimiento, que muchas veces se abre una sola vez desde un celular, ese costo de primera carga es
el peor caso. Además, todo lo que el componente necesite quedaría expuesto en el navegador. Se
descartó por duplicar trabajo sin beneficio.

**Interactive Auto.** Empieza en Server y después pasa a WebAssembly. Suma los costos de los dos
modos (circuito, API aparte, descarga del runtime) para resolver un problema de latencia de
interacción que estas pantallas no tienen.

**Mezclar modos por componente.** Blazor permite que una página SSR tenga un componente interactivo
puntual. No se adopta ahora porque no hay ningún caso que lo pida, pero queda como salida si aparece
uno (ver Consecuencias).

## Consecuencias

Sobre los cuatro ejes que pide la letra:

| Eje | Static SSR (elegido) | Interactive Server (descartado) |
| --- | --- | --- |
| Escalado horizontal | Cualquier instancia atiende cualquier pedido; no requiere afinidad de sesión | Circuito atado a una instancia; requiere afinidad de sesión y pierde el estado si la instancia cae |
| Manejo de estado | Sin estado en el servidor entre pedidos; lo necesario viaja en el formulario, la URL o la cookie de sesión | Estado del componente en la memoria del servidor mientras dure el circuito |
| Latencia | Un pedido HTTP por acción con recarga de página; el seguimiento es cacheable y responde rápido | Clics con poca latencia por WebSocket, pero dependientes de la conexión; una conexión inestable en el celular corta el circuito |
| Recursos del servidor | CPU solo mientras se atiende el pedido; sin conexiones abiertas | Memoria y una conexión abierta por usuario conectado |

Positivas:

- **El aislamiento funciona igual que en el resto de la Web:** el mismo middleware, el mismo
  `TenantActualHttp` y los mismos filtros de EF Core, sin un segundo mecanismo que mantener.
- **El seguimiento público se puede proteger con lo que ya existe:** limitación de tasa por IP
  (`[EnableRateLimiting]` sobre el componente), cabeceras seguras con una CSP estricta (la página no
  necesita scripts propios), `no-store` y caché en Redis por envío.
- **Compatible con el escalado de la sección 6.13** y con el escenario 1 de la defensa.
- **Formularios sin JavaScript propio** y con el mismo antiforgery que Razor Pages: el botón "Salir"
  del portal postea a la Razor Page de login sin adaptaciones.

Negativas, y lo que la decisión dificulta a futuro:

- **Cada acción recarga la página.** No hay validación mientras se escribe ni actualización
  automática: el comercio tiene que recargar para ver un aviso nuevo.
- **Algunos detalles del form handling de Blazor se resuelven a mano.** Los checkboxes con el mismo
  nombre se leen de `Request.Form`, los parámetros de query que son enums se reciben como texto, y cada
  formulario necesita un `@formname` único (`desactivar-{id}`, `reenviar-{id}`).
- **Si una pantalla del portal necesitara tiempo real** (por ejemplo, ver el avance de los envíos sin
  recargar), habría que darle `@rendermode` solo a ese componente y resolver el tenant del circuito
  como se describe en la primera alternativa. La decisión no lo impide, pero ese trabajo queda pendiente.
- **Hay que respetar la regla en las páginas nuevas:** un componente del portal con `@rendermode`
  rompe el supuesto de aislamiento. Hoy `NuevoEnvio.razor` la incumple y está pendiente migrarlo.

## Verificación

- Los componentes del portal y del seguimiento no declaran `@rendermode` (revisable con
  `grep -rn "@rendermode" src/UltimaMilla.Web`; el único resultado esperado, mientras no se migre, es
  `NuevoEnvio.razor`).
- Probado con la aplicación levantada: un usuario anónimo en `/portal` va al login; el comercio ve solo
  sus envíos y suscripciones; los formularios de alta, baja y reenvío funcionan con antiforgery; un
  parámetro manipulado en la URL no se muestra.
- El aislamiento que este modo preserva lo cubren las pruebas de integración del panel de avisos
  (`PostgresTests.Avisos`) y de seguimiento (`CuentaTenantTests`).
