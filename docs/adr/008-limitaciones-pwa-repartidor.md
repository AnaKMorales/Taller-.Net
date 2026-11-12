# ADR-008: limitaciones de la PWA del repartidor

Estado: aceptada (noviembre 2026). Documenta límites conocidos y las decisiones tomadas para la
entrega actual. Revisar si se decide pasar a una app nativa o un contenedor.

> Las marcas **[NO VERIFICADO]** señalan comportamientos de navegadores o plataformas que no se
> confirmaron leyendo el código de este repositorio ni probándolos en un dispositivo. Antes de
> tomarlos como ciertos, hay que confirmarlos en la documentación oficial o probarlos.

## Contexto

La app del repartidor es una PWA en Blazor WebAssembly (`src/UltimaMilla.Repartidor`). Se eligió
porque funciona sin conexión, se instala desde el navegador sin tienda de apps y comparte C# con el
resto de la solución ([ADR-005](005-sincronizacion-offline-repartidor.md)). A cambio, depende de lo
que permita cada navegador. Este ADR reúne esos límites, lo que pasa hoy en el código y qué se
decidió para cada uno.

## Decisión

Para esta entrega se mantiene la PWA. Ninguno de estos límites impide la operación básica
(descargar la hoja, registrar visitas sin conexión y sincronizarlas), así que se aceptan y se
mitigan como se indica abajo.

### 1. GPS con la pantalla apagada

- **Qué pasa:** con la pantalla apagada o la app minimizada, el navegador suspende la página y no
  entrega posiciones. Sin una app nativa no hay seguimiento continuo del recorrido. [NO VERIFICADO:
  no hay ninguna API web de geolocalización en segundo plano.]
- **Qué hace hoy el código:** pide una lectura puntual al registrar la visita
  (`wwwroot/js/repartidor.js`, `ubicacion()`: alta precisión, espera de 10 s y lecturas de hasta
  60 s de antigüedad). La posición viaja con el intento y con cada evidencia. Al abrir una parada,
  `Componentes/AvisoUbicacion.razor` prueba el GPS. Si falla, distingue "permiso denegado" de "sin
  señal" y ofrece "Reintentar ubicación" (`Dispositivo/LecturaUbicacion.cs`). Las evidencias sin
  posición no se pueden guardar (`ValidadorAccion`).
- **Impacto:** cada entrega o fallo tiene su ubicación, pero no hay un recorrido entre paradas. Si
  el repartidor deniega el permiso, no puede guardar fotos ni firmas.
- **Decisión:** posición **por acción**. Si se necesita un recorrido continuo, la alternativa es una
  app nativa o un contenedor (por ejemplo, .NET MAUI) con permiso de ubicación en segundo plano.

### 2. Cifrado de IndexedDB

- **Qué pasa:** la sesión (con el token Bearer), la hoja de ruta (nombres y direcciones de los
  destinatarios) y la cola con fotos y firmas se guardan en IndexedDB **sin cifrar**
  (`repartidor.js`: `leer`, `guardar`, `borrar`). Quien tenga el dispositivo desbloqueado, o acceso a
  los archivos del perfil del navegador, puede leerlos.
- **Por qué el problema es la clave:** cifrar con WebCrypto (AES-GCM) es fácil. Lo difícil es dónde
  guardar la clave:
  - Si se guarda en el mismo IndexedDB, aunque sea una `CryptoKey` no exportable, el que tiene el
    dispositivo también tiene la clave. Solo protege contra quien lee los archivos en crudo.
  - Si se deriva de un PIN (PBKDF2), el repartidor tiene que ingresarlo cada vez que abre la app sin
    conexión. Si lo olvida, las acciones pendientes no se pueden descifrar ni enviar.
  - Si la manda el servidor, la app no puede abrir los datos sin conexión, que es justo para lo que
    existe.
- **Impacto:** riesgo de exposición de datos personales si se pierde o se roba un celular. El token
  dura 8 horas y el servidor verifica en cada pedido que el usuario siga activo (ADR-005), así que
  desactivar al usuario corta el acceso al servidor. Los datos que ya están en el dispositivo siguen
  legibles.
- **Decisión:** **deuda documentada**. Para cerrarla: PIN de la app con clave derivada (PBKDF2),
  migración de los datos ya guardados y una regla clara para el PIN olvidado. Estimado: 4 a 6 h,
  más pruebas en dispositivos.

### 3. Escaneo de bultos con la cámara

- **Qué pasa:** hoy no hay escaneo con cámara. `Componentes/RegistrarBultos.razor` tiene un campo de
  código que funciona con un lector físico que actúa como teclado, o tipeando el código.
- **Soporte de navegadores:**
  - [NO VERIFICADO] `BarcodeDetector` (nativo) está en Chrome y Edge para Android, macOS y ChromeOS.
    No está en Firefox ni en Safari, incluido iOS. En Windows y Linux puede faltar.
  - [NO VERIFICADO] Una librería en JavaScript, como ZXing, funciona en cualquier navegador con
    `getUserMedia` (HTTPS y permiso de cámara). Para que ande sin conexión hay que incluirla dentro
    de la PWA para que el service worker la guarde.
- **Impacto:** sin lector físico, el repartidor tipea el código o elige el resultado de cada bulto
  en la lista. Es más lento, pero no bloquea la entrega.
- **Decisión:** **mejora documentada**. Si se hace: `BarcodeDetector` cuando exista y ZXing como
  respaldo, las dos detrás del mismo método de `Navegador`, y el resultado entra por el mismo
  camino que el campo de código. Estimado: 3 a 5 h.

### 4. Pérdida del almacenamiento local

- **Qué pasa:** las acciones existen solo en IndexedDB hasta que se sincronizan. Se pierden si el
  usuario borra los datos del sitio, si desinstala la PWA o si el navegador libera espacio.
  [NO VERIFICADO: los navegadores pueden borrar datos de sitios cuando falta espacio, salvo que el
  sitio tenga almacenamiento persistente.] El código no llama a `navigator.storage.persist()` (no
  aparece en `repartidor.js`).
- **Impacto:** se pueden perder visitas registradas pero no enviadas, con sus fotos y firmas. Es el
  riesgo más serio, porque son hechos operativos.
- **Mitigación actual:** sincronizar cuanto antes. Se intenta al registrar, cada 30 s y al volver la
  red. La barra muestra cuántas acciones quedan pendientes, y no se puede cerrar sesión con
  pendientes (ADR-005).
- **Decisión:** se acepta para esta entrega. Mejora siguiente, chica: pedir
  `navigator.storage.persist()` al iniciar sesión y avisar si el navegador lo niega.

### 5. iOS (Safari)

- **Qué pasa:** [NO VERIFICADO] en iOS todos los navegadores usan el motor de Safari. Safari puede
  borrar los datos de un sitio que no se usó en varios días si no está instalado en la pantalla de
  inicio. No tiene Background Sync ni `BarcodeDetector`. El código no tiene ningún ajuste propio
  para iOS.
- **Impacto:** en iPhone la app funciona mientras está abierta, pero la sincronización ocurre solo
  con la app en primer plano, y el riesgo del punto 4 es mayor.
- **Decisión:** recomendar **instalar la PWA en la pantalla de inicio** y abrirla al empezar el
  turno. Para la flota se recomiendan celulares Android con Chrome. No se hacen ajustes propios
  para iOS en esta entrega.

### 6. Notificaciones push

- **Qué pasa:** la PWA no tiene notificaciones push. No hay suscripción a push ni manejador `push`
  en el service worker (`wwwroot/service-worker.published.js`), y el servidor no tiene endpoint de
  push. [NO VERIFICADO: en iOS el push web existe solo para PWAs instaladas, desde iOS 16.4.]
- **Impacto:** si el despachador cambia la ruta (saca una parada o reprograma), el repartidor se
  entera solo cuando la hoja se descarga de nuevo, por ejemplo después de sincronizar. Mientras
  tanto puede registrar algo que el servidor va a rechazar. Ese caso ya está cubierto por la
  política "el servidor gana" del ADR-005.
- **Decisión:** no se implementa push en esta entrega. Si se agrega, hace falta Web Push con claves
  VAPID, un endpoint de suscripción en Web y un aviso desde el Worker cuando cambie una ruta
  despachada.

## Alternativas consideradas

| Alternativa | A favor | En contra | Decisión |
| --- | --- | --- | --- |
| Mantener la PWA y documentar los límites | Ya funciona sin conexión, no necesita tienda de apps y comparte C# y contratos | GPS solo por acción, almacenamiento no garantizado, iOS limitado | **Elegida** para esta entrega |
| App nativa o .NET MAUI | GPS en segundo plano, almacenamiento seguro (Keychain/Keystore), push y cámara confiables | Otro proyecto que mantener, publicación en tiendas y mucho más tiempo del que hay | Para el futuro, si se necesita seguimiento continuo |
| Cifrar ahora con la clave en el mismo IndexedDB | Rápido | Da una falsa sensación de seguridad: la clave está al lado de los datos | Descartada |
| Implementar ahora escaneo y push | Mejor experiencia | No son necesarios para la demo y suman riesgo de último momento | Postergada |

## Consecuencias

- La operación básica (hoja, visitas sin conexión, sincronización sin duplicados) funciona en
  Android con Chrome, y también en escritorio para la demo.
- Quedan como deuda conocida: el cifrado local (punto 2), el almacenamiento persistente (punto 4),
  el escaneo con cámara (punto 3) y el push (punto 6).
- Hay que probar los puntos marcados [NO VERIFICADO] en dispositivos reales antes de pasar a
  producción, en especial iOS.
- Si un requisito futuro pide GPS continuo, push confiable en iOS o almacenamiento cifrado sin PIN,
  la respuesta probable es una app nativa o un contenedor, y habría que escribir un ADR nuevo.
