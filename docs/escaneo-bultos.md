# Escaneo de bultos con la cámara (PWA del repartidor)

Letra: requisito 19 (escaneo de bultos) y 6.11 ("escaneo de códigos de barras o QR"; para equipos de tres la app
puede ser una PWA).

## Qué hay

- **Etiqueta con código de barras.** Cada etiqueta de bulto del portal (`/portal/envios/{código}/etiqueta`) imprime
  un **Code 128** con el código del bulto, generado como SVG en el servidor (`Web/Compartido/CodigoBarras128.cs`):
  sin librerías ni recursos externos, se imprime sin internet. El SVG solo tiene rectángulos calculados; el texto
  del código nunca entra en el marcado. Si el código tiene caracteres fuera del juego B (por ejemplo "ñ"), la
  etiqueta queda solo con el texto.
- **Escáner en la PWA.** En el registro de la visita (`Componentes/RegistrarBultos.razor`), el botón
  **Escanear con la cámara** (`Componentes/EscanerCodigo.razor`) abre la cámara trasera y usa `BarcodeDetector`, la
  API nativa del navegador, cuando existe. Si no (Windows, iPhone, Firefox), decodifica los cuadros del video con
  ZXing (`wwwroot/lib/zxing`, MIT), servido por la propia PWA y guardado por el service worker. En los dos casos
  funciona **sin conexión**. Lee Code 128, QR, Code 39, EAN,
  ITF, UPC y Data Matrix. Cada código leído hace lo mismo que escribirlo y confirmarlo: marca el bulto como entregado
  si pertenece a la visita, o avisa que no pertenece. El mismo código no se repite antes de 2 s y el teléfono vibra
  al leer.
- **Faltantes y sobrantes.** Desde la primera lectura, la pantalla muestra «Escaneados X de N» y dos avisos que se
  actualizan con cada código (`Dispositivo/ControlEscaneo.cs`): **faltan escanear** (los códigos de los bultos de la
  visita que todavía no se leyeron) y **sobrantes** (los códigos leídos que no son de esta visita, una sola vez cada
  uno). Si un bulto se lee de nuevo, avisa que ya estaba escaneado. Cuando no falta ninguno, lo confirma en verde.
  Es informativo: no bloquea registrar la visita (puede haber bultos no entregados o marcados a mano). La cámara se apaga al cerrar o al salir de la pantalla.
- **Permiso de cámara.** La cabecera `Permissions-Policy` permite `camera=(self)` solo bajo `/repartidor` y
  `/backoffice/deposito`; el resto del sitio sigue con `camera=()` y el micrófono bloqueado en todos lados.
- Se mantienen el ingreso manual y los lectores externos (que tipean el código).

## Límites

- La cámara exige HTTPS (o `localhost`). Sin cámara o con el permiso denegado queda el ingreso manual o un lector
  externo.
- ZXing es más lento que `BarcodeDetector` (lee unos 4 cuadros por segundo a 640 px): conviene acercar bien el código.
- La PWA escanea al **entregar**. La carga del vehículo se escanea en el backoffice (ver abajo), que se abre desde
  el celular o la tablet del depósito.

## Escaneo al cargar el vehículo (requisito 19)

En **Depósito → Ruta** (`/backoffice/deposito/rutas/{id}`), el operario escanea cada bulto al subirlo al vehículo y
el sistema lo verifica contra la hoja de ruta:

- **Escanear con la cámara** usa el mismo lector que la PWA (`wwwroot/js/escaner-carga.js`: `BarcodeDetector` o
  ZXing). Cada código leído se registra solo; después de cada bulto la cámara se vuelve a abrir y el mismo código se
  ignora unos segundos para no registrarlo dos veces. También sirven el lector externo y el tipeo.
- **Sobrante:** si el bulto no está en la hoja de ruta, `RegistrarCargaRutaHandler` lanza `BultoSobranteException`
  y no registra nada. La pantalla muestra el aviso y suma el código a la lista de **Sobrantes**, que queda a la vista
  hasta despachar.
- **Faltantes:** la pantalla lista los bultos de la hoja de ruta que todavía no se cargaron, con su código, y los
  repite al despachar ("Faltan cargar 2 bultos: …").
- La cabecera `Permissions-Policy` permite `camera=(self)` también en `/backoffice/deposito`.
- Pruebas: `CargaDeUnBultoQueNoEstaEnLaHojaDeRutaAvisaSobrante` (unitaria), el paso del sobrante en
  `ElDepositoRecibeAsignaCargaYDespacha` (E2E) y `SoloLaPwaYElDepositoPuedenUsarLaCamara` (cabeceras).
- Verificación: tests del estándar (tabla de 107 símbolos, sumas, paridad, dígito de control), del SVG y de la
  búsqueda del bulto; y una etiqueta real renderizada a imagen y leída de nuevo desde sus píxeles ("EXP-ADM-01",
  control correcto). [NO VERIFICADO] con un decodificador independiente ni con un celular real: probar en la demo.
