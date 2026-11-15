# Cifrado en reposo de la PWA del repartidor

Letra 6.3: "Cifrado en reposo de los datos personales almacenados en el dispositivo móvil".

## Qué se cifra

Todo lo que la PWA guarda en IndexedDB pasa por `leer`/`guardar`/`borrar` de
`src/UltimaMilla.Repartidor/wwwroot/js/repartidor.js`, y ahí se cifra. Eso incluye:

- la **sesión** (token de acceso y datos del repartidor);
- la **hoja de ruta** (nombres, direcciones y teléfonos de los destinatarios);
- la **cola de acciones** sin sincronizar (resultados, nombre y documento de quien recibe, fotos y firmas);
- el identificador del dispositivo.

El código C# no cambió: sigue guardando y leyendo texto; el cifrado es transparente.

## Cómo

- **AES-GCM de 256 bits** con Web Crypto (`crypto.subtle`), nativo del navegador y disponible sin conexión. En
  Blazor WebAssembly el `AesGcm` de .NET no está soportado, por eso se hace en JavaScript.
- **Clave del dispositivo, no exportable.** Se genera la primera vez con `extractable: false` y se guarda como
  `CryptoKey` en su propio almacén (`claves`) de la misma base. El código puede usarla, pero ni la app ni un script
  pueden leer sus bytes (`exportKey` falla).
- **Un IV aleatorio de 12 bytes por cada guardado.** Se guarda `{ formato, iv, datos }`.
- **El nombre del dato como dato asociado (AAD).** Un registro cifrado copiado a otro nombre, o con un byte
  alterado, no se puede descifrar.
- **Migración sin pérdida.** La base pasa a la versión 2 (agrega `claves`) y conserva los datos. Un valor guardado
  en texto por la versión anterior se lee igual y se reemplaza por su versión cifrada, **solo si nadie lo cambió
  entretanto** (para no pisar acciones nuevas de la cola).
- **Si algo no se puede descifrar** (clave perdida o dato alterado), se trata como ausente y se informa en la
  consola: la app sigue funcionando (volver a ingresar o a descargar la hoja) en lugar de quedar trabada.

## Prueba

`tests/Navegador/probar-cifrado-pwa.sh` abre Chrome real (headless, perfil temporal) con el `repartidor.js` del
repositorio y verifica: migración desde una base de la versión anterior, ida y vuelta, que el texto no aparece en
lo guardado, clave AES-GCM 256 no exportable, IV distinto en cada guardado, rechazo de registros movidos o
alterados, borrado, una cola grande con foto, y que la clave persiste al recargar.

## Límites (qué protege y qué no)

- Protege los datos **copiados del almacenamiento** del navegador (un respaldo, una copia del perfil, otra app o
  herramienta que lea los archivos de IndexedDB): ahí solo hay datos cifrados.
- **No** protege de quien usa la propia app con el dispositivo desbloqueado, ni de código malicioso que corra en
  el mismo origen: ese código puede pedirle a la app que descifre. La clave queda en el almacenamiento del
  navegador, protegida por él y por el bloqueo del dispositivo, no por una contraseña del usuario.
- [NO VERIFICADO] la extracción de la clave no exportable copiando los archivos internos del perfil del navegador:
  depende de cómo cada navegador la persiste.
- Si se borran los datos del sitio, se pierden la clave y los datos juntos (como antes, se pierden las acciones
  no sincronizadas).
- [NO VERIFICADO] el uso simultáneo en dos pestañas durante la migración; la creación de la clave sí contempla la
  carrera (`add` y relectura).
