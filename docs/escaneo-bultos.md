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
  al leer. La cámara se apaga al cerrar o al salir de la pantalla.
- **Permiso de cámara.** La cabecera `Permissions-Policy` permite `camera=(self)` solo bajo `/repartidor`; el resto
  del sitio sigue con `camera=()` y el micrófono bloqueado en todos lados.
- Se mantienen el ingreso manual y los lectores externos (que tipean el código).

## Límites

- La cámara exige HTTPS (o `localhost`). Sin cámara o con el permiso denegado queda el ingreso manual o un lector
  externo.
- ZXing es más lento que `BarcodeDetector` (lee unos 4 cuadros por segundo a 640 px): conviene acercar bien el código.
- La PWA escanea al **entregar**. La carga del vehículo se registra desde el backoffice (Depósito → Ruta), con
  lector externo o tipeo; esa pantalla no usa la cámara.
- Verificación: tests del estándar (tabla de 107 símbolos, sumas, paridad, dígito de control), del SVG y de la
  búsqueda del bulto; y una etiqueta real renderizada a imagen y leída de nuevo desde sus píxeles ("EXP-ADM-01",
  control correcto). [NO VERIFICADO] con un decodificador independiente ni con un celular real: probar en la demo.
