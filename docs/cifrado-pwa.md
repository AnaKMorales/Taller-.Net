# Cifrado en reposo de la PWA del repartidor

El acceso local de la PWA cifra la sesión y su token, la hoja de ruta, las visitas pendientes
con sus fotos y firmas, las posiciones sin sincronizar y el identificador del dispositivo.
El punto 9 de la reentrega (H5) aplica a la ubicación de la clave que protege esos datos.

## Acceso local

Al abrir la PWA actualizada por primera vez, el repartidor configura una frase local de al
menos 12 caracteres. La app vuelve a pedirla tras cerrar, recargar o bloquear el dispositivo.
El desbloqueo usa Web Crypto y funciona sin red una vez que la PWA publicada quedó disponible
en la caché del service worker. El inicio de sesión con el servidor sigue siendo necesario
para autenticar una cuenta o renovar un token vencido.

`wwwroot/js/almacen-local.js` deriva una clave AES-GCM de 256 bits con PBKDF2-SHA-256,
600.000 iteraciones y una sal aleatoria de 16 bytes. En el almacén `proteccion` quedan la
versión, la sal y un verificador cifrado. La frase y la clave derivada permanecen fuera de
IndexedDB. La PWA conserva la clave no exportable en memoria mientras está abierta. Cada
registro tiene un IV aleatorio de 12 bytes y autentica su nombre como dato asociado.

## Migración

IndexedDB pasa a la versión 3 al cargar la PWA actualizada. Al configurar la frase, los
registros anteriores en texto claro o cifrados se convierten al nuevo formato. Una transacción
guarda los datos migrados y la configuración a la vez que vacía el almacén anterior de claves.
Si falla un descifrado o la escritura, los registros anteriores se conservan para reintentar.
Una base nueva solo contiene `datos` y `proteccion`. Una base migrada puede conservar el
almacén `claves` vacío.

## Verificación local

`tests/Navegador/probar-cifrado-pwa.sh` usa Chrome y Python 3. Comprueba el bloqueo al
recargar, el rechazo de una frase incorrecta, la recuperación de la sesión y una visita
pendiente con la frase correcta, el borrado de la clave antigua y la conservación de datos
si falla la migración. También verifica IV distintos y el rechazo de registros cifrados
movidos de nombre o alterados.

Para una demo con la PWA publicada, preparar una hoja de ruta, registrar una visita sin red
y tocar **Bloquear**. Con la red todavía cortada, intentar una frase incorrecta y después la
correcta. Al abrirse la app, la misma visita debe seguir pendiente. En las herramientas del
navegador, inspeccionar `proteccion` y comprobar que `claves` no exista o esté vacío.

## Alcance

La protección depende de la frase elegida y cubre el almacenamiento persistido después de
la migración. El acceso a una página ya desbloqueada, la ejecución de código en el mismo
origen y copias hechas con la versión anterior quedan fuera de esa protección. Si se
olvida la frase o se borran los datos del sitio, las visitas que quedaron solo en el
dispositivo no se pueden recuperar desde el servidor.
